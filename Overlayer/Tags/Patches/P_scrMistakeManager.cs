using Overlayer.Core.Patches;
using System;

namespace Overlayer.Tags.Patches;

public class P_scrMistakeManager : PatchBase<P_scrMistakeManager> {
    [LazyPatch("Tags.P_scrMistakeManager.AccuracyStats__CalculatePercentAcc", "scrMistakesManager", "CalculateTotalAccuracy", Triggers =
    [
        nameof(AccuracyStats.Accuracy), nameof(AccuracyStats.MaxAccuracy),
        nameof(AccuracyStats.XAccuracy), nameof(AccuracyStats.MaxXAccuracy),
        nameof(AccuracyStats.AbsXAccuracy), nameof(AccuracyStats.AbsMaxXAccuracy),
        nameof(Scores.XScore), nameof(Scores.MaxXScore)
    ])]
    public static class AccuracyStats__CalculatePercentAcc {
        public static void Postfix(scrMistakesManager __instance) {
            // scrMarginTracker.CalculatePercentAcc() (called on every hit, before this
            // postfix runs) already computes percentAcc/percentXAcc/maxPossibleXAcc/xScore
            // using the game's own up-to-date weights, so read those directly instead of
            // re-deriving them here. This is what keeps midspin/autoplay excluded from
            // X-Accuracy (game r150+): HitMarginHelper.PlayerHitMarginWeights simply has no
            // entry for HitMargin.Auto/Midspin/Multipress/OverPress, so GetHitsWithWeights
            // and playerHitMarginCount never count them.
            var tracker = ADOBase.controller.playerOne.marginTracker;

            double checkpointMinus = Math.Pow(0.9875, scrController.checkpointsUsed);

            AccuracyStats.Accuracy = 100.0 * tracker.percentAcc;
            AccuracyStats.XAccuracy = 100.0 * tracker.percentXAcc;
            AccuracyStats.AbsXAccuracy = AccuracyStats.XAccuracy / checkpointMinus;
            AccuracyStats.AbsMaxXAccuracy = 100.0 * tracker.maxPossibleXAcc;
            AccuracyStats.MaxXAccuracy = AccuracyStats.AbsMaxXAccuracy * checkpointMinus;

            Scores.XScore = tracker.xScore;
            Scores.MaxXScore = tracker.maxXScore;

            if(ADOBase.lm is not null && ADOBase.lm.listFloors != null &&
                Tile.CurTile >= 0 && Tile.CurTile < ADOBase.lm.listFloors.Count &&
                ADOBase.lm.listFloors[Tile.CurTile] != null) {

                // Mirrors scrMarginTracker.CalculatePercentAcc()'s HitMarginHelper.PerfectHitMargins /
                // SemiPerfectHitMargins split, extended with the tiles left in the level as guaranteed
                // future perfects, to estimate the best Accuracy still reachable from here.
                int perfectAll = tracker.GetHits(HitMargin.PerfectMinus, HitMargin.XPerfect, HitMargin.PerfectPlus, HitMargin.Auto, HitMargin.Midspin, HitMargin.Multipress);
                int semiPerfect = tracker.GetHits(HitMargin.EarlyPerfect, HitMargin.LatePerfect);
                int failedFloor = tracker.GetHits(HitMargin.FailedFloor);
                int deaths = tracker.GetDeaths();

                int lefttile = Tile.LeftTile - (ADOBase.lm.listFloors[Tile.CurTile].midSpin ? 1 : 0);

                int mxsucess = lefttile + perfectAll + semiPerfect;
                int mxtotal = tracker.hitMargins.Count + lefttile + deaths;
                double mxratio = (mxsucess == mxtotal) ? 1.0 : ((double)mxsucess / mxtotal);
                double mxbonus = ((lefttile + perfectAll) * 0.0001) - (failedFloor * 0.0001);

                AccuracyStats.MaxAccuracy = 100.0 * (mxratio + mxbonus);
            }
        }
    }
}
