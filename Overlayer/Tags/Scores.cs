using Overlayer.Tags.Attributes;

namespace Overlayer.Tags;

public static class Scores {
    [Tag]
    [TagDesc("Lenient difficulty score")]
    public static int LScore;
    [Tag]
    [TagDesc("Normal difficulty score")]
    public static int NScore;
    [Tag]
    [TagDesc("Strict difficulty score")]
    public static int SScore;
    [Tag]
    [TagDesc("Current difficulty score")]
    public static int Score;
    [Tag]
    [TagDesc("X-Score(2 per XPerfect, 1 per -Perfect/Perfect+, 0 otherwise)")]
    public static int XScore;
    [Tag]
    [TagDesc("Maximum possible X-Score for this level((Total Tile - Autoplay - Midspin) x 2)")]
    public static int MaxXScore;

    public static void SetScores(HitMargin l, HitMargin n, HitMargin s, HitMargin c) {
        switch(c) {
            case HitMargin.VeryEarly:
            case HitMargin.VeryLate:
                Score += 91;
                break;
            case HitMargin.EarlyPerfect:
            case HitMargin.LatePerfect:
                Score += 150;
                break;
            case HitMargin.PerfectMinus:
            case HitMargin.XPerfect:
            case HitMargin.PerfectPlus:
                Score += 300;
                break;
        }
        switch(l) {
            case HitMargin.VeryEarly:
            case HitMargin.VeryLate:
                LScore += 91;
                break;
            case HitMargin.EarlyPerfect:
            case HitMargin.LatePerfect:
                LScore += 150;
                break;
            case HitMargin.PerfectMinus:
            case HitMargin.XPerfect:
            case HitMargin.PerfectPlus:
                LScore += 300;
                break;
        }
        switch(n) {
            case HitMargin.VeryEarly:
            case HitMargin.VeryLate:
                NScore += 91;
                break;
            case HitMargin.EarlyPerfect:
            case HitMargin.LatePerfect:
                NScore += 150;
                break;
            case HitMargin.PerfectMinus:
            case HitMargin.XPerfect:
            case HitMargin.PerfectPlus:
                NScore += 300;
                break;
        }
        switch(s) {
            case HitMargin.VeryEarly:
            case HitMargin.VeryLate:
                SScore += 91;
                break;
            case HitMargin.EarlyPerfect:
            case HitMargin.LatePerfect:
                SScore += 150;
                break;
            case HitMargin.PerfectMinus:
            case HitMargin.XPerfect:
            case HitMargin.PerfectPlus:
                SScore += 300;
                break;
        }
    }

    public static void Reset() => LScore = NScore = SScore = Score = XScore = MaxXScore = 0;
}
