using Overlayer.Tags.Attributes;

namespace Overlayer.Tags;

public static class AccuracyStats {
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Accuracy(when perfect: 100+0.01%)")]
    public static double Accuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("XAccuracy(when pure perfect: 100)")]
    public static double XAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Absolute Max XAccuracy(Excluding checkpoints)")]
    public static double AbsXAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable Accuracy in the current tile")]
    public static double MaxAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable XAccuracy in the current tile")]
    public static double MaxXAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable Absolute Max XAccuracy in the current tile(Excluding checkpoints)")]
    public static double AbsMaxXAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Accuracy computed the same way on every ADOFAI version, regardless of which one is running")]
    public static double CAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("XAccuracy computed the same way on every ADOFAI version (Auto/Midspin never count), regardless of which one is running")]
    public static double CXAccuracy;

    public static void Reset() => Accuracy = XAccuracy = MaxAccuracy = AbsXAccuracy = MaxXAccuracy = AbsMaxXAccuracy = CAccuracy = CXAccuracy = double.NaN;
}
