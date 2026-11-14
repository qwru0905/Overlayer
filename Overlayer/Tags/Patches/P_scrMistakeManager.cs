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
    [LazyPatch("Tags.P_scrMistakeManager.AccuracyStats__CalculatePercentAcc", "scrMistakesManager", "CalculateTotalAccuracy", MinVersion = GameVersion.ScrPlayerRefactor, Triggers =
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

        // Tile.TotalTile = ADOBase.lm.listFloors.Count counts every floor object, including index
        // 0 - but scrLevelMaker.PlayerHitFloors (the game's own "tiles that actually get hit"
        // list) explicitly filters to `seqID > 0`, since the player starts standing on tile 0 and
        // never "hits into" it. So LeftTile = TotalTile - CurTile is always 1 higher than the
        // number of tiles still to hit. rawLeftTile must come from an UP-TO-DATE Tile.CurTile -
        // see MaxAccuracyDeferral for why that isn't the case while a hit is still being resolved.
        // Without the -1, MaxAccuracy credits one extra guaranteed-future-perfect that doesn't
        // exist, inflating it by perfectAll-bonus's one unit (0.0001 -> +0.01 percentage points)
        // right when it should drop to exactly match Accuracy. Math.Max guards leftTile never
        // going negative.
        public static double ComputeMaxAccuracy(int perfectAll, int semiPerfect, int failedFloor, int deaths, int totalHits, int rawLeftTile) {
            int leftTile = Math.Max(0, rawLeftTile - 1);
            int mxsucess = leftTile + perfectAll + semiPerfect;
            int mxtotal = totalHits + leftTile + deaths;
            double mxratio = (mxsucess == mxtotal) ? 1.0 : ((double)mxsucess / mxtotal);
            double mxbonus = ((leftTile + perfectAll) * 0.0001) - (failedFloor * 0.0001);
            return 100.0 * (mxratio + mxbonus);
        }

        // Everything except the tracker read is shared with the Legacy patch - see CStats.
        public static void ApplyVersionIndependentStats(scrMarginTracker tracker) => CStats.Apply(name => GameCount(tracker, name));

        // NoInlining for the same reason as ApplyAlphaOnlyXScoreFields: the closure captures a
        // scrMarginTracker, which doesn't exist on r136.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Func<string, int> CounterForCurrentTracker() {
            var tracker = ADOBase.controller?.playerOne?.marginTracker;
            return tracker == null ? null : name => GameCount(tracker, name);
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

                MaxAccuracyDeferral.Update(perfectAll, semiPerfect, failedFloor, deaths, tracker.hitMargins.Count);
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
    // plays above, just typed as scrMistakesManager instead of scrMarginTracker; the two are
    // version-gated apart (MinVersion/MaxVersion), so they never coexist on one build and the
    // patch validator doesn't report the other one's missing target on every launch.
    [LazyPatch("Tags.P_scrMistakeManager.AccuracyStats__CalculatePercentAcc.Legacy", "scrMistakesManager", "CalculatePercentAcc", MaxVersion = GameVersion.ScrPlayerRefactor - 1, Triggers =
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

        public static void ApplyVersionIndependentStats(scrMistakesManager tracker) => CStats.Apply(CounterFor(tracker));

        internal static Func<string, int> CounterFor(scrMistakesManager tracker) => name => GameCount(tracker, name);

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

                MaxAccuracyDeferral.Update(perfectAll, semiPerfect, failedFloor, deaths, GetHitMarginsCount());
            }

            ApplyVersionIndependentStats(tracker);
        }
    }

    // CAccuracy/CXAccuracy combine Overlayer's own Hit.C* counters (reset on every rewind by
    // TagResetter) with counts only the game's tracker has (Auto, Multipress, the fail kinds). The
    // tracker is NOT reset on a checkpoint restart, only rolled back to the checkpoint - so mixing
    // the two raw made CXAccuracy read 0% right after a checkpoint restart (0 hits / N old fails).
    // scrMistakesManager.RevertToLastCheckpoint runs on every (re)start, after the tracker's own
    // reset/rollback and after Awake_Rewind reset Hit.C*, on every supported build; snapshotting
    // the tracker there gives CStats a baseline to subtract, so both sides count from the same
    // moment.
    [LazyPatch("Tags.P_scrMistakeManager.CStats__RevertToLastCheckpoint", "scrMistakesManager", "RevertToLastCheckpoint", Triggers =
    [
        nameof(AccuracyStats.CAccuracy), nameof(AccuracyStats.CXAccuracy)
    ])]
    public static class CStats__RevertToLastCheckpoint {
        public static void Postfix(scrMistakesManager __instance) {
            var counter = GameVersion.HasScrPlayer
                ? AccuracyStats__CalculatePercentAcc.CounterForCurrentTracker()
                : AccuracyStats__CalculatePercentAcc_Legacy.CounterFor(__instance);
            if(counter == null) {
                return;
            }
            CStats.CaptureBaseline(counter);
            // Nothing recalculates accuracy between this and the first hit, so refresh now
            // instead of showing values computed against the previous baseline until then.
            CStats.Apply(counter);
        }
    }
}

// The version-independent C* accuracy stats, shared by both CalculateTotalAccuracy (145+) and
// CalculatePercentAcc (r136) patches - they differ only in how the game's tracker is read, which
// the caller passes in as `count` (HitMargin name -> that build's recorded count; 0 for a margin
// the build doesn't have).
internal static class CStats {
    private static int baseAuto, baseMidspin, baseMultipress, baseFailMiss, baseFailOverload, baseFailedFloor;

    public static void CaptureBaseline(Func<string, int> count) {
        baseAuto = count(nameof(HitMargin.Auto));
        baseMidspin = count(nameof(HitMargin.Midspin));
        baseMultipress = count(nameof(HitMargin.Multipress));
        baseFailMiss = count(nameof(HitMargin.FailMiss));
        baseFailOverload = count(nameof(HitMargin.FailOverload));
        baseFailedFloor = count(nameof(HitMargin.FailedFloor));
    }

    // Recorded by the game since the last CaptureBaseline - see CStats__RevertToLastCheckpoint.
    // Clamped: a baseline taken before the tracker's own reset would otherwise go negative until
    // the next capture.
    private static int Since(Func<string, int> count, string name, int baseline) => Math.Max(0, count(name) - baseline);

    public static void Apply(Func<string, int> count) {
        int autoCount = Since(count, nameof(HitMargin.Auto), baseAuto);
        // Pre-150 has no Midspin member at all - the game records a midspin as a plain Perfect -
        // so fall back to Overlayer's own count of the same thing (Hit.CMidspin).
        int midspinCount = GameMargin.TryGet(nameof(HitMargin.Midspin)) != null
            ? Since(count, nameof(HitMargin.Midspin), baseMidspin)
            : Hit.CMidspin;
        int multipressCount = Since(count, nameof(HitMargin.Multipress), baseMultipress);
        int failMissCount = Since(count, nameof(HitMargin.FailMiss), baseFailMiss);
        int failOverloadCount = Since(count, nameof(HitMargin.FailOverload), baseFailOverload);
        int failedFloorCount = Since(count, nameof(HitMargin.FailedFloor), baseFailedFloor);
        // GetDeaths() is FailMiss + FailOverload on every supported build (confirmed in the
        // 150/148/136 decompiles), so derive it from the baselined counts too.
        int deaths = failMissCount + failOverloadCount;

        // An overloading press is classified TooEarly by Overlayer (Hit.CTE) but the game records
        // it as FailOverload INSTEAD of TooEarly - counting both would put the one press in the
        // denominator twice on top of its death.
        int tooEarly = Math.Max(0, Hit.CTE - failOverloadCount);

        // Mirrors HitMarginHelper.PlayerHitMarginWeights: XPerfect/PerfectMinus/PerfectPlus
        // and the forced-perfect override states (Auto/Midspin/Multipress) = full success;
        // EarlyPerfect/LatePerfect = full success too for CAccuracy (mirrors
        // HitMarginHelper.PerfectHitMargins/SemiPerfectHitMargins, alpha's plain-Accuracy
        // formula - see spec §5's table), but only 0.75-weighted for CXAccuracy.
        int perfectFamily = Hit.CP + autoCount + midspinCount + multipressCount;
        int semiPerfect = Hit.CEP + Hit.CLP;

        int accDenominator = tooEarly + Hit.CVE + Hit.CEP + Hit.CP + Hit.CLP + Hit.CVL + Hit.CTL
            + autoCount + midspinCount + multipressCount + failMissCount + failOverloadCount + failedFloorCount + deaths;
        int accNumerator = perfectFamily + semiPerfect;
        double accRatio = accDenominator == 0 ? 0.0 : (accNumerator == accDenominator ? 1.0 : (double)accNumerator / accDenominator);
        double accBonus = (perfectFamily * 0.0001) - (failedFloorCount * 0.0001);
        AccuracyStats.CAccuracy = 100.0 * (accRatio + accBonus);

        // Alpha's own percentXAcc also applies this checkpoint discount (Math.Pow(0.9875,
        // checkpointsUsed)) before it's read into AccuracyStats.XAccuracy - match it here too,
        // so CXAccuracy tracks XAccuracy's philosophy (checkpoint-discounted) rather than
        // AbsXAccuracy's (checkpoint-free). Alpha skips the discount in the level editor; not
        // replicated here (checkpointsUsed is 0 outside a real playthrough anyway).
        double xWeighted = (Hit.CP * 1.0) + ((Hit.CEP + Hit.CLP) * 0.75) + ((Hit.CVE + Hit.CVL) * 0.4) + ((tooEarly + Hit.CTL) * 0.2) + (failedFloorCount * 0.2);
        int xDenominator = tooEarly + Hit.CVE + Hit.CEP + Hit.CP + Hit.CLP + Hit.CVL + Hit.CTL + failedFloorCount + failMissCount + failOverloadCount;
        double checkpointMinus = Math.Pow(0.9875, scrController.checkpointsUsed);
        AccuracyStats.CXAccuracy = xDenominator == 0 ? 0.0 : 100.0 * (xWeighted / xDenominator) * checkpointMinus;

        // Mirrors HitMarginHelper.HitMarginXScores exactly: XPerfect=2, PerfectMinus/PerfectPlus=1, everything else=0.
        Scores.CXScore = (Hit.CXP * 2) + Hit.CPM + Hit.CPP;
    }
}

// MaxAccuracy needs Tile.CurTile, which P_scrPlanet.Tile__SwitchChosen only updates in a Postfix
// on scrPlanet.SwitchChosen - but a successful hit's AddHit (and so the accuracy patches above)
// runs from INSIDE SwitchChosen, before it advances controller.currentSeqID. Inside SwitchChosen
// CurTile is therefore one tile behind for a hit that lands, but already right for one that's
// rejected (too early, overload) - and nothing at AddHit time says which of the two it is. So
// while SwitchChosen is running, only remember the inputs and let Tile__SwitchChosen's Postfix
// compute MaxAccuracy once CurTile is current. Outside SwitchChosen (Fail2Action on death,
// tracker resets) CurTile is already current, so compute immediately.
internal static class MaxAccuracyDeferral {
    public static bool InSwitchChosen;
    private static bool pending;
    private static int perfectAll, semiPerfect, failedFloor, deaths, totalHits;

    public static void Update(int perfectAll, int semiPerfect, int failedFloor, int deaths, int totalHits) {
        MaxAccuracyDeferral.perfectAll = perfectAll;
        MaxAccuracyDeferral.semiPerfect = semiPerfect;
        MaxAccuracyDeferral.failedFloor = failedFloor;
        MaxAccuracyDeferral.deaths = deaths;
        MaxAccuracyDeferral.totalHits = totalHits;
        pending = true;
        if(!InSwitchChosen) {
            Flush();
        }
    }

    public static void Flush() {
        if(!pending) {
            return;
        }
        pending = false;
        AccuracyStats.MaxAccuracy = P_scrMistakeManager.AccuracyStats__CalculatePercentAcc.ComputeMaxAccuracy(
            perfectAll, semiPerfect, failedFloor, deaths, totalHits, Tile.LeftTile);
    }
}
