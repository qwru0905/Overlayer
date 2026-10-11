using HarmonyLib;
using Overlayer.Core.Patches;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Overlayer.Tags.Patches;

public class P_scrMistakeManager : PatchBase<P_scrMistakeManager> {
    [LazyPatch("Tags.P_scrMistakeManager.AccuracyStats__CalculatePercentAcc", "scrMistakesManager", "CalculateTotalAccuracy", Triggers =
    [
        nameof(AccuracyStats.Accuracy), nameof(AccuracyStats.MaxAccuracy),
        nameof(AccuracyStats.XAccuracy), nameof(AccuracyStats.MaxXAccuracy),
        nameof(AccuracyStats.AbsXAccuracy), nameof(AccuracyStats.AbsMaxXAccuracy),
        nameof(Scores.XScore), nameof(Scores.MaxXScore),
        nameof(AccuracyStats.CAccuracy), nameof(AccuracyStats.CXAccuracy), nameof(Scores.CXScore),

        // Dependency - InternalProfile.IT_Santokki_XAccuracy reads AccuracyStats.XAccuracy directly
        nameof(InternalProfile.IT_Santokki_XAccuracy)
    ])]
    public static class AccuracyStats__CalculatePercentAcc {
        // Only called from Postfix when GameVersion.Current >= 150. Mono resolves a method's IL
        // as a whole the first time it's CALLED, not per branch - xScore/maxXScore/maxPossibleXAcc
        // don't exist at all on scrMarginTracker pre-150 (confirmed directly in the 148 decompile,
        // not just a numbering difference), so a reference to them anywhere in a method that DOES
        // get called on 148/145 would fail even on a branch that's never taken. Keeping them in a
        // separate, conditionally-CALLED method is what actually protects against that.
        // NoInlining: if Mono ever inlined this into Postfix, the whole point of splitting it out
        // (so 148/145 never JIT-resolves the alpha-only xScore/maxXScore/maxPossibleXAcc members)
        // would silently stop working. Cheap insurance for a correctness property this file exists
        // to guarantee.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ApplyAlphaOnlyXScoreFields(scrMarginTracker tracker, double checkpointMinus) {
            AccuracyStats.AbsMaxXAccuracy = 100.0 * tracker.maxPossibleXAcc;
            AccuracyStats.MaxXAccuracy = AccuracyStats.AbsMaxXAccuracy * checkpointMinus;
            Scores.XScore = tracker.xScore;
            // tracker.maxXScore (the game's own property) is `base.lm.PlayerHitFloors.Count * ...`
            // with no null check on lm - confirmed in the r150 decompile. scrMarginTracker.Reset()
            // (called from scrController.Start(), before ADOBase.lm exists) ends by calling
            // CalculateTotalAccuracy() itself, which runs this Postfix while lm is still null,
            // so reading maxXScore here unconditionally throws the game's own NullReferenceException.
            Scores.MaxXScore = ADOBase.lm != null ? tracker.maxXScore : 0;
        }

        static int GameCount(scrMarginTracker tracker, string name) => GameMargin.TryGet(name) is HitMargin m ? tracker.GetHits(m) : 0;

        // Two independent off-by-ones stack up in Tile.LeftTile by the time this runs, both
        // confirmed against the real r150 decompile:
        //   1. Staleness: Tile.CurTile/LeftTile are updated by P_scrPlanet.Tile__SwitchChosen, a
        //      Postfix on the OUTER scrPlanet.SwitchChosen - but this patch (on
        //      scrMistakesManager.CalculateTotalAccuracy) runs from INSIDE SwitchChosen, via the
        //      AddHit() call partway through it, well before SwitchChosen reassigns
        //      controller.currentSeqID near its end. So Tile.CurTile here is always one hit behind:
        //      it still shows the PREVIOUS hit's tile, not the one just resolved.
        //   2. Tile.TotalTile = ADOBase.lm.listFloors.Count counts every floor object, including
        //      index 0 - but scrLevelMaker.PlayerHitFloors (the game's own "tiles that actually get
        //      hit" list) explicitly filters to `seqID > 0`, since the player starts standing on
        //      tile 0 and never "hits into" it. So Tile.CurTile's true ceiling is TotalTile - 1, not
        //      TotalTile, meaning LeftTile = TotalTile - CurTile is always 1 higher than the real
        //      tile count even with zero staleness.
        // Together that's leftTile = rawLeftTile - 2 at the true end of a level. Without both
        // corrections, MaxAccuracy credits one extra guaranteed-future-perfect that doesn't exist,
        // inflating it by perfectAll-bonus's one unit (0.0001 -> +0.01 percentage points) right when
        // it should drop to exactly match Accuracy. Math.Max guards leftTile never going negative.
        public static double ComputeMaxAccuracy(int perfectAll, int semiPerfect, int failedFloor, int deaths, int totalHits, int rawLeftTile) {
            int leftTile = Math.Max(0, rawLeftTile - 2);
            int mxsucess = leftTile + perfectAll + semiPerfect;
            int mxtotal = totalHits + leftTile + deaths;
            double mxratio = (mxsucess == mxtotal) ? 1.0 : ((double)mxsucess / mxtotal);
            double mxbonus = ((leftTile + perfectAll) * 0.0001) - (failedFloor * 0.0001);
            return 100.0 * (mxratio + mxbonus);
        }

        // Split out from Postfix so a test harness can exercise it directly without a live
        // scrController/ADOBase.controller - everything it reads is either Hit's own static
        // counters (self-consistent on every build) or GameMargin-resolved state via the
        // tracker passed in (also safe on every build, unlike a raw HitMargin literal).
        public static void ApplyVersionIndependentStats(scrMarginTracker tracker) {
            int autoCount = GameCount(tracker, nameof(HitMargin.Auto));
            int midspinCount = GameCount(tracker, nameof(HitMargin.Midspin)); // 0 pre-150: that build's Auto slot already merged this in, see spec §2
            int multipressCount = GameCount(tracker, nameof(HitMargin.Multipress));
            int failMissCount = GameCount(tracker, nameof(HitMargin.FailMiss));
            int failOverloadCount = GameCount(tracker, nameof(HitMargin.FailOverload));
            int failedFloorCount = GameCount(tracker, nameof(HitMargin.FailedFloor));
            int deaths = tracker.GetDeaths();

            // Mirrors HitMarginHelper.PlayerHitMarginWeights: XPerfect/PerfectMinus/PerfectPlus
            // and the forced-perfect override states (Auto/Midspin/Multipress) = full success;
            // EarlyPerfect/LatePerfect = full success too for CAccuracy (mirrors
            // HitMarginHelper.PerfectHitMargins/SemiPerfectHitMargins, alpha's plain-Accuracy
            // formula - see spec §5's table), but only 0.75-weighted for CXAccuracy.
            int perfectFamily = Hit.CP + autoCount + midspinCount + multipressCount;
            int semiPerfect = Hit.CEP + Hit.CLP;

            int accDenominator = Hit.CTE + Hit.CVE + Hit.CEP + Hit.CP + Hit.CLP + Hit.CVL + Hit.CTL
                + autoCount + midspinCount + multipressCount + failMissCount + failOverloadCount + failedFloorCount + deaths;
            int accNumerator = perfectFamily + semiPerfect;
            double accRatio = accDenominator == 0 ? 0.0 : (accNumerator == accDenominator ? 1.0 : (double)accNumerator / accDenominator);
            double accBonus = (perfectFamily * 0.0001) - (failedFloorCount * 0.0001);
            AccuracyStats.CAccuracy = 100.0 * (accRatio + accBonus);

            // Alpha's own percentXAcc also applies this checkpoint discount (Math.Pow(0.9875,
            // checkpointsUsed)) before it's read into AccuracyStats.XAccuracy - match it here too,
            // so CXAccuracy tracks XAccuracy's philosophy (checkpoint-discounted) rather than
            // AbsXAccuracy's (checkpoint-free). Alpha also skips this in the level editor
            // (scnEditor.instance) - not replicated here on purpose: scnEditor derives from
            // UnityEngine.Object, and touching its .instance would make this method (deliberately
            // kept callable without a live scrController/ADOBase.controller - see the class
            // comment) hit the same native-icall wall as everything else that touches a
            // UnityEngine.Object-derived instance outside the real engine. checkpointsUsed is a
            // plain static field, not itself Object-derived, and is 0 outside a real playthrough
            // anyway, so this only matters in the editor specifically, where CXAccuracy isn't a
            // meaningful metric to begin with.
            double xWeighted = (Hit.CP * 1.0) + ((Hit.CEP + Hit.CLP) * 0.75) + ((Hit.CVE + Hit.CVL) * 0.4) + ((Hit.CTE + Hit.CTL) * 0.2) + (failedFloorCount * 0.2);
            int xDenominator = Hit.CTE + Hit.CVE + Hit.CEP + Hit.CP + Hit.CLP + Hit.CVL + Hit.CTL + failedFloorCount + failMissCount + failOverloadCount;
            double checkpointMinus = Math.Pow(0.9875, scrController.checkpointsUsed);
            AccuracyStats.CXAccuracy = xDenominator == 0 ? 0.0 : 100.0 * (xWeighted / xDenominator) * checkpointMinus;

            // Mirrors HitMarginHelper.HitMarginXScores exactly: XPerfect=2, PerfectMinus/PerfectPlus=1, everything else=0.
            Scores.CXScore = (Hit.CXP * 2) + Hit.CPM + Hit.CPP;
        }

        public static void Postfix(scrMistakesManager __instance) {
            // scrMarginTracker.CalculatePercentAcc() (called on every hit, before this
            // postfix runs) already computes percentAcc/percentXAcc using the game's own
            // up-to-date weights, so read those directly instead of re-deriving them here.
            // percentAcc/percentXAcc exist and are populated on every in-scope build (145/148/150);
            // maxPossibleXAcc/xScore/maxXScore are alpha-only - see ApplyAlphaOnlyXScoreFields.
            var tracker = ADOBase.controller?.playerOne?.marginTracker;
            if(tracker == null) {
                return;
            }

            double checkpointMinus = Math.Pow(0.9875, scrController.checkpointsUsed);

            AccuracyStats.Accuracy = 100.0 * tracker.percentAcc;
            AccuracyStats.XAccuracy = 100.0 * tracker.percentXAcc;
            AccuracyStats.AbsXAccuracy = AccuracyStats.XAccuracy / checkpointMinus;

            if(GameVersion.Current >= 150) {
                ApplyAlphaOnlyXScoreFields(tracker, checkpointMinus);
            } else {
                AccuracyStats.AbsMaxXAccuracy = double.NaN;
                AccuracyStats.MaxXAccuracy = double.NaN;
                Scores.XScore = 0;
                Scores.MaxXScore = 0;
            }

            if(ADOBase.lm is not null && ADOBase.lm.listFloors != null &&
                Tile.CurTile >= 0 && Tile.CurTile < ADOBase.lm.listFloors.Count &&
                ADOBase.lm.listFloors[Tile.CurTile] != null) {

                // Mirrors scrMarginTracker.CalculatePercentAcc()'s HitMarginHelper.PerfectHitMargins /
                // SemiPerfectHitMargins split, extended with the tiles left in the level as guaranteed
                // future perfects, to estimate the best Accuracy still reachable from here.
                // GameMargin.Existing resolves each name against the ACTUAL loaded HitMargin
                // (correct numbering on every build) and drops PerfectMinus/XPerfect/PerfectPlus/
                // Midspin/FailedFloor entirely on builds where they don't exist as named members -
                // the raw HitMargin literal this used before could read the wrong slot (e.g.
                // LatePerfect's alpha value lands on TooLate's slot on 148) or throw
                // IndexOutOfRangeException outright (Midspin/FailedFloor's alpha values are past
                // the end of 148's shorter enum). "Perfect" is pre-150's own name for the whole
                // un-split perfect window (148's CalculatePercentAcc counts it the same way alpha
                // counts PerfectMinus/XPerfect/PerfectPlus together) - it resolves to nothing on
                // alpha (where the split replaced it), so this is a no-op there.
                int perfectAll = tracker.GetHits(GameMargin.Existing(nameof(HitMargin.PerfectMinus), nameof(HitMargin.XPerfect), nameof(HitMargin.PerfectPlus), "Perfect", nameof(HitMargin.Auto), nameof(HitMargin.Midspin), nameof(HitMargin.Multipress)));
                int semiPerfect = tracker.GetHits(GameMargin.Existing(nameof(HitMargin.EarlyPerfect), nameof(HitMargin.LatePerfect)));
                int failedFloor = tracker.GetHits(GameMargin.Existing(nameof(HitMargin.FailedFloor)));
                int deaths = tracker.GetDeaths();

                AccuracyStats.MaxAccuracy = ComputeMaxAccuracy(perfectAll, semiPerfect, failedFloor, deaths, tracker.hitMargins.Count, Tile.LeftTile);
            }

            ApplyVersionIndependentStats(tracker);
        }
    }

    // older-2.9.8 (r136) predates the scrMarginTracker/scrPlayer split entirely: scrMistakesManager
    // itself still carries everything AccuracyStats__CalculatePercentAcc reads off `tracker` above -
    // percentAcc/percentXAcc (instance fields), hitMargins/hitMarginsCount (static), GetHits/
    // GetDeaths (instance methods) - and the method being patched is still named CalculatePercentAcc
    // (confirmed via decompile; ca3c732 renamed it to CalculateTotalAccuracy going forward, general/
    // older (145/148) only have the new name). So __instance here plays exactly the role `tracker`
    // plays above, just typed as scrMistakesManager instead of scrMarginTracker; the two never
    // coexist on one build (LazyPatch.Patch() no-ops whichever target doesn't resolve), so there's
    // no risk of double-applying these stats.
    [LazyPatch("Tags.P_scrMistakeManager.AccuracyStats__CalculatePercentAcc.Legacy", "scrMistakesManager", "CalculatePercentAcc", Triggers =
    [
        nameof(AccuracyStats.Accuracy), nameof(AccuracyStats.MaxAccuracy),
        nameof(AccuracyStats.XAccuracy), nameof(AccuracyStats.MaxXAccuracy),
        nameof(AccuracyStats.AbsXAccuracy), nameof(AccuracyStats.AbsMaxXAccuracy),
        nameof(Scores.XScore), nameof(Scores.MaxXScore),
        nameof(AccuracyStats.CAccuracy), nameof(AccuracyStats.CXAccuracy), nameof(Scores.CXScore),
        nameof(InternalProfile.IT_Santokki_XAccuracy)
    ])]
    public static class AccuracyStats__CalculatePercentAcc_Legacy {
        // GetHits/GetDeaths/hitMargins no longer exist on scrMistakesManager at all on the build
        // this project compiles against (fully moved to scrMarginTracker, not left as a compat
        // shim) - direct calls wouldn't even compile. Resolved by name instead, which only needs
        // them to exist on whatever's actually loaded at runtime (confirmed on 2.9.8's decompile).
        private static MethodInfo getHitsMethod;
        private static int GetHits(scrMistakesManager tracker, HitMargin m) {
            getHitsMethod ??= AccessTools.Method(typeof(scrMistakesManager), "GetHits", [typeof(HitMargin)]);
            return getHitsMethod == null ? 0 : (int)getHitsMethod.Invoke(tracker, [m]);
        }
        // scrMarginTracker.GetHits(HitMargin[]) sums hits across several margins in one call (see
        // the non-Legacy Postfix above); the old scrMistakesManager.GetHits only ever took one
        // HitMargin at a time (confirmed via decompile), so the array form is summed here instead.
        private static int GetHits(scrMistakesManager tracker, HitMargin[] margins) {
            int total = 0;
            foreach(var m in margins) {
                total += GetHits(tracker, m);
            }

            return total;
        }
        private static MethodInfo getDeathsMethod;
        private static int GetDeaths(scrMistakesManager tracker) {
            getDeathsMethod ??= AccessTools.Method(typeof(scrMistakesManager), "GetDeaths");
            return getDeathsMethod == null ? 0 : (int)getDeathsMethod.Invoke(tracker, null);
        }
        private static FieldInfo hitMarginsField;
        private static int GetHitMarginsCount() {
            hitMarginsField ??= AccessTools.Field(typeof(scrMistakesManager), "hitMargins");
            return hitMarginsField?.GetValue(null) is List<HitMargin> list ? list.Count : 0;
        }
        // percentAcc/percentXAcc ARE on scrMistakesManager on both builds (confirmed via decompile:
        // `public float percentAcc;` on 2.9.8) - but as a plain field there and an auto-property on
        // the build this project compiles against, so a direct `tracker.percentAcc` compiles to
        // `get_percentAcc()`, which throws MissingMethodException on 2.9.8 (confirmed live: this was
        // the very first exception right after reaching the main menu, before any level even
        // loaded - everything downstream that depends on menu/level setup finishing cleanly ends up
        // half-initialized once this throws uncaught). Same GetMember-by-kind fix as everywhere else.
        private static MemberInfo percentAccMember;
        private static MemberInfo percentXAccMember;
        private static float GetPercentAcc(scrMistakesManager tracker) {
            percentAccMember ??= typeof(scrMistakesManager).GetMember("percentAcc", BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
            return percentAccMember switch {
                PropertyInfo p => (float)p.GetValue(tracker),
                FieldInfo f => (float)f.GetValue(tracker),
                _ => 0f
            };
        }
        private static float GetPercentXAcc(scrMistakesManager tracker) {
            percentXAccMember ??= typeof(scrMistakesManager).GetMember("percentXAcc", BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
            return percentXAccMember switch {
                PropertyInfo p => (float)p.GetValue(tracker),
                FieldInfo f => (float)f.GetValue(tracker),
                _ => 0f
            };
        }

        static int GameCount(scrMistakesManager tracker, string name) => GameMargin.TryGet(name) is HitMargin m ? GetHits(tracker, m) : 0;

        public static void ApplyVersionIndependentStats(scrMistakesManager tracker) {
            int autoCount = GameCount(tracker, nameof(HitMargin.Auto));
            int midspinCount = GameCount(tracker, nameof(HitMargin.Midspin));
            int multipressCount = GameCount(tracker, nameof(HitMargin.Multipress));
            int failMissCount = GameCount(tracker, nameof(HitMargin.FailMiss));
            int failOverloadCount = GameCount(tracker, nameof(HitMargin.FailOverload));
            int failedFloorCount = GameCount(tracker, nameof(HitMargin.FailedFloor));
            int deaths = GetDeaths(tracker);

            int perfectFamily = Hit.CP + autoCount + midspinCount + multipressCount;
            int semiPerfect = Hit.CEP + Hit.CLP;

            int accDenominator = Hit.CTE + Hit.CVE + Hit.CEP + Hit.CP + Hit.CLP + Hit.CVL + Hit.CTL
                + autoCount + midspinCount + multipressCount + failMissCount + failOverloadCount + failedFloorCount + deaths;
            int accNumerator = perfectFamily + semiPerfect;
            double accRatio = accDenominator == 0 ? 0.0 : (accNumerator == accDenominator ? 1.0 : (double)accNumerator / accDenominator);
            double accBonus = (perfectFamily * 0.0001) - (failedFloorCount * 0.0001);
            AccuracyStats.CAccuracy = 100.0 * (accRatio + accBonus);

            double xWeighted = (Hit.CP * 1.0) + ((Hit.CEP + Hit.CLP) * 0.75) + ((Hit.CVE + Hit.CVL) * 0.4) + ((Hit.CTE + Hit.CTL) * 0.2) + (failedFloorCount * 0.2);
            int xDenominator = Hit.CTE + Hit.CVE + Hit.CEP + Hit.CP + Hit.CLP + Hit.CVL + Hit.CTL + failedFloorCount + failMissCount + failOverloadCount;
            double checkpointMinus = Math.Pow(0.9875, scrController.checkpointsUsed);
            AccuracyStats.CXAccuracy = xDenominator == 0 ? 0.0 : 100.0 * (xWeighted / xDenominator) * checkpointMinus;

            Scores.CXScore = (Hit.CXP * 2) + Hit.CPM + Hit.CPP;
        }

        public static void Postfix(scrMistakesManager __instance) {
            var tracker = __instance;

            double checkpointMinus = Math.Pow(0.9875, scrController.checkpointsUsed);

            AccuracyStats.Accuracy = 100.0 * GetPercentAcc(tracker);
            AccuracyStats.XAccuracy = 100.0 * GetPercentXAcc(tracker);
            AccuracyStats.AbsXAccuracy = AccuracyStats.XAccuracy / checkpointMinus;

            // Pre-150 (every build this Legacy patch can even resolve on): xScore/maxXScore/
            // maxPossibleXAcc don't exist - mirrors the GameVersion.Current>=150 else-branch above.
            AccuracyStats.AbsMaxXAccuracy = double.NaN;
            AccuracyStats.MaxXAccuracy = double.NaN;
            Scores.XScore = 0;
            Scores.MaxXScore = 0;

            if(ADOBase.lm is not null && ADOBase.lm.listFloors != null &&
                Tile.CurTile >= 0 && Tile.CurTile < ADOBase.lm.listFloors.Count &&
                ADOBase.lm.listFloors[Tile.CurTile] != null) {

                int perfectAll = GetHits(tracker, GameMargin.Existing(nameof(HitMargin.PerfectMinus), nameof(HitMargin.XPerfect), nameof(HitMargin.PerfectPlus), "Perfect", nameof(HitMargin.Auto), nameof(HitMargin.Midspin), nameof(HitMargin.Multipress)));
                int semiPerfect = GetHits(tracker, GameMargin.Existing(nameof(HitMargin.EarlyPerfect), nameof(HitMargin.LatePerfect)));
                int failedFloor = GetHits(tracker, GameMargin.Existing(nameof(HitMargin.FailedFloor)));
                int deaths = GetDeaths(tracker);

                AccuracyStats.MaxAccuracy = AccuracyStats__CalculatePercentAcc.ComputeMaxAccuracy(perfectAll, semiPerfect, failedFloor, deaths, GetHitMarginsCount(), Tile.LeftTile);
            }

            ApplyVersionIndependentStats(tracker);
        }
    }
}
