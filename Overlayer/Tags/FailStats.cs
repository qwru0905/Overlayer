using HarmonyLib;
using Overlayer.Tags.Attributes;
using System.Reflection;

namespace Overlayer.Tags;

public static class FailStats {
    static readonly FieldInfo multipressCounterField = AccessTools.Field(typeof(scrFailBar), "multipressCounter");

    static float GetMultipressCounter(scrFailBar failBar) => (float)multipressCounterField.GetValue(failBar);

    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The percentage of the overload gauge.")]
    public static float OverloadCounter() {
        var controller = scrController.instance;
        var failBar = controller?.playerOne?.failBar;
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
        var failBar = controller?.playerOne?.failBar;
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
    public static float OverloadCounterRaw => scrController.instance?.playerOne?.failBar?.overloadCounter ?? float.NaN;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The raw internal value of the multipress gauge in the game.")]
    public static float MultipressCounterRaw {
        get {
            var failBar = scrController.instance?.playerOne?.failBar;
            return failBar == null ? float.NaN : GetMultipressCounter(failBar);
        }
    }
}
