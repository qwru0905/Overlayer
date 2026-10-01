using Overlayer.Core.Patches;
using Overlayer.Utils;
using System;
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
            Scores.MaxXScore = tracker.maxXScore;
        }

        static int GameCount(scrMarginTracker tracker, string name) => GameMargin.TryGet(name) is HitMargin m ? tracker.GetHits(m) : 0;

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

                int lefttile = Tile.LeftTile - (ADOBase.lm.listFloors[Tile.CurTile].midSpin ? 1 : 0);

                int mxsucess = lefttile + perfectAll + semiPerfect;
                int mxtotal = tracker.hitMargins.Count + lefttile + deaths;
                double mxratio = (mxsucess == mxtotal) ? 1.0 : ((double)mxsucess / mxtotal);
                double mxbonus = ((lefttile + perfectAll) * 0.0001) - (failedFloor * 0.0001);

                AccuracyStats.MaxAccuracy = 100.0 * (mxratio + mxbonus);
            }

            ApplyVersionIndependentStats(tracker);
        }
    }
}
