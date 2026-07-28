using MonsterLove.StateMachine;
using Overlayer.Core.Patches;
using System;

namespace Overlayer.Tags.Patches;

public class M_MonsterLove : PatchBase<M_MonsterLove> {
    [LazyPatch("Tags.M_MonsterLove.Tile__ChangeState__StateMachine__StateBehaviour", "MonsterLove.StateMachine.StateBehaviour", "ChangeState", ["System.Enum"], Triggers =
    [
        nameof(Tile.IsStarted), nameof(Tile.StartTile), nameof(Tile.StartProgress),

        // Dependency
        nameof(AccuracyStats.MaxXAccuracy), nameof(AccuracyStats.AbsMaxXAccuracy)
    ])]
    public static class Tile__ChangeState__StateMachine__StateBehaviour__ChangeState {
        public static void Prefix(StateBehaviour __instance, Enum newState) {
            if(__instance is not scrController ctrl) {
                return;
            }
            var cur = __instance.stateMachine.GetState();
            if(cur != newState && (States)newState == States.PlayerControl) {
                Tile.IsStarted = true;

                // ADOBase.isPlayingLevel was removed by the game update; approximate it with
                // "gameplay is currently unpaused" (its closest surviving equivalent).
                if(ADOBase.isScnGame || (ADOBase.controller?.paused ?? true)) {
                    return;
                }
                Tile.SetStartValues(ctrl, ctrl.currentSeqID);
            }
        }
    }
}