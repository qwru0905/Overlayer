using HarmonyLib;
using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System.Reflection;

namespace Overlayer.Tags;

public static class FailStats {
    static readonly FieldInfo multipressCounterField = AccessTools.Field(typeof(scrFailBar), "multipressCounter");

    static float GetMultipressCounter(scrFailBar failBar) => (float)multipressCounterField.GetValue(failBar);

    // failbar moved from a plain public field directly on scrController to a property on the new
    // per-player scrPlayer (ctrl.playerOne.failBar) in the r147 refactor; scrFailBar itself (and
    // multipressCounterField above, which is already name-based reflection) is unaffected either
    // way. Split like every other scrPlayer-gated accessor so only the path for the running build
    // ever gets JIT'd.
    private static scrFailBar GetFailBarViaPlayer(scrController ctrl) => ctrl.playerOne?.failBar;
    private static AccessTools.FieldRef<scrController, scrFailBar> failBarRef;
    private static scrFailBar GetFailBarDirect(scrController ctrl) {
        failBarRef ??= AccessTools.FieldRefAccess<scrController, scrFailBar>("failbar");
        return failBarRef(ctrl);
    }
    private static scrFailBar GetFailBar(scrController ctrl) =>
        GameVersion.HasScrPlayer ? GetFailBarViaPlayer(ctrl) : GetFailBarDirect(ctrl);

    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The percentage of the overload gauge.")]
    public static float OverloadCounter() {
        var controller = scrController.instance;
        var failBar = controller ? GetFailBar(controller) : null;
        return controller == null
            ? float.NaN
            : failBar == null
            ? float.NaN
            : IsImmortal(controller) ? 100f : CalculateFailValue(failBar.overloadCounter);
    }

    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The percentage of the multipress gauge.")]
    public static float MultipressCounter() {
        var controller = scrController.instance;
        var failBar = controller ? GetFailBar(controller) : null;
        return !controller
            ? float.NaN
            : failBar == null
            ? float.NaN
            : IsImmortal(controller) ? 100f : CalculateFailValue(GetMultipressCounter(failBar));
    }

    public static bool IsImmortal(scrController controller)
        => ADOBase.isOfficialLevel && controller.gameworld && controller.percentComplete >= 0.96f;

    public static float CalculateFailValue(float value)
        => value > 1f ? 0f : (1f - value) * 100f;

    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The raw internal value of the overload gauge in the game.")]
    public static float OverloadCounterRaw {
        get {
            var controller = scrController.instance;
            var failBar = controller ? GetFailBar(controller) : null;
            return failBar?.overloadCounter ?? float.NaN;
        }
    }
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The raw internal value of the multipress gauge in the game.")]
    public static float MultipressCounterRaw {
        get {
            var controller = scrController.instance;
            var failBar = controller ? GetFailBar(controller) : null;
            return failBar == null ? float.NaN : GetMultipressCounter(failBar);
        }
    }
}
