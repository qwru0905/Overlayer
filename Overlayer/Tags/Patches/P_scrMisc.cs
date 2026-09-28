using Overlayer.Core.Patches;
using System;

namespace Overlayer.Tags.Patches;

// The game replaced scrMisc.GetHitMargin with two methods that both take the Difficulty:
//   GetHitMarginInDeg - classic path, works on angles
//   GetHitMarginInSec - async-input path, works on a time offset in seconds
// Their parameter lists differ (Harmony binds by name), so each patch needs one class per method.
// The actual logic is shared through the helpers below.
public class P_scrMisc : PatchBase<P_scrMisc> {
    private static bool IsFreeroam(scrController controller) => controller && controller.currFloor.freeroam;

    private static void ApplyHit(scrController controller, HitMargin l, HitMargin n, HitMargin s, ref HitMargin result) {
        Hit.Lenient = l;
        Hit.Normal = n;
        Hit.Strict = s;
        Hit.FixMargin(controller, ref Hit.Lenient);
        Hit.FixMargin(controller, ref Hit.Normal);
        Hit.FixMargin(controller, ref Hit.Strict);
        Hit.Current = result = Hit.GetCHit(GCS.difficulty);
        if(!Hit.ControllerIsSafe(controller)) {
            Hit.IncreaseCount(Difficulty.Lenient, Hit.Lenient);
            Hit.IncreaseCount(Difficulty.Normal, Hit.Normal);
            Hit.IncreaseCount(Difficulty.Strict, Hit.Strict);
            Hit.IncreaseCCount(Hit.Current);
        }
    }

    private static void ApplyCombo(scrController controller, HitMargin l, HitMargin n, HitMargin s, HitMargin result) {
        if(Hit.ControllerIsSafe(controller)) {
            return;
        }
        // PerfectMinus/XPerfect/PerfectPlus all sit inside the old single "Perfect!" window
        // (game r150+ split it three ways), so any of them should keep the combo alive.
        if(result is HitMargin.PerfectMinus or HitMargin.XPerfect or HitMargin.PerfectPlus) {
            ComboStats.MaxCombo = Math.Max(ComboStats.MaxCombo, ++ComboStats.Combo);
        } else {
            ComboStats.Combo = 0;
        }
        Hit.FixMargin(controller, ref l);
        Hit.FixMargin(controller, ref n);
        Hit.FixMargin(controller, ref s);
        Scores.SetScores(l, n, s, result);
        ComboStats.Combos_Set(Difficulty.Lenient, l);
        ComboStats.Combos_Set(Difficulty.Normal, n);
        ComboStats.Combos_Set(Difficulty.Strict, s);
        ComboStats.SetMarginCombos();
    }

    private static void ApplyScores(scrController controller, HitMargin l, HitMargin n, HitMargin s, HitMargin result) {
        if(Hit.ControllerIsSafe(controller)) {
            return;
        }
        Hit.FixMargin(controller, ref l);
        Hit.FixMargin(controller, ref n);
        Hit.FixMargin(controller, ref s);
        Scores.SetScores(l, n, s, result);
    }

    #region Hit
    [LazyPatch("Tags.P_scrMisc.Hit__GetHitMarginInDeg", "scrMisc", "GetHitMarginInDeg", Triggers =
    [
        nameof(Hit.LHit), nameof(Hit.LTE), nameof(Hit.LVE), nameof(Hit.LEP), nameof(Hit.LPM), nameof(Hit.LXP), nameof(Hit.LPP), nameof(Hit.LP), nameof(Hit.LLP), nameof(Hit.LVL), nameof(Hit.LTL),
        nameof(Hit.NHit), nameof(Hit.NTE), nameof(Hit.NVE), nameof(Hit.NEP), nameof(Hit.NPM), nameof(Hit.NXP), nameof(Hit.NPP), nameof(Hit.NP), nameof(Hit.NLP), nameof(Hit.NVL), nameof(Hit.NTL),
        nameof(Hit.SHit), nameof(Hit.STE), nameof(Hit.SVE), nameof(Hit.SEP), nameof(Hit.SPM), nameof(Hit.SXP), nameof(Hit.SPP), nameof(Hit.SP), nameof(Hit.SLP), nameof(Hit.SVL), nameof(Hit.STL),
        nameof(Hit.CHit), nameof(Hit.CTE), nameof(Hit.CVE), nameof(Hit.CEP), nameof(Hit.CPM), nameof(Hit.CXP), nameof(Hit.CPP), nameof(Hit.CP), nameof(Hit.CLP), nameof(Hit.CVL), nameof(Hit.CTL),
        nameof(Hit.LT),   nameof(Hit.LV),  nameof(Hit.LELP),
        nameof(Hit.NT),   nameof(Hit.NV),  nameof(Hit.NELP),
        nameof(Hit.ST),   nameof(Hit.SV),  nameof(Hit.SELP),
        nameof(Hit.CT),   nameof(Hit.CV),  nameof(Hit.CELP),
        "LHitRaw", "NHitRaw", "SHitRaw", "CHitRaw",
        nameof(Hit.LFast), nameof(Hit.NFast), nameof(Hit.SFast), nameof(Hit.CFast),
        nameof(Hit.LSlow), nameof(Hit.NSlow), nameof(Hit.SSlow), nameof(Hit.CSlow)
    ])]
    public static class Hit__GetHitMarginInDeg {
        public static bool Prefix(float hitAngle, float refAngle, bool clockwise, float floorBpm, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return true;
            }
            ApplyHit(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                ref __result);
            return false;
        }
    }

    [LazyPatch("Tags.P_scrMisc.Hit__GetHitMarginInSec", "scrMisc", "GetHitMarginInSec", Triggers =
    [
        nameof(Hit.LHit), nameof(Hit.LTE), nameof(Hit.LVE), nameof(Hit.LEP), nameof(Hit.LPM), nameof(Hit.LXP), nameof(Hit.LPP), nameof(Hit.LP), nameof(Hit.LLP), nameof(Hit.LVL), nameof(Hit.LTL),
        nameof(Hit.NHit), nameof(Hit.NTE), nameof(Hit.NVE), nameof(Hit.NEP), nameof(Hit.NPM), nameof(Hit.NXP), nameof(Hit.NPP), nameof(Hit.NP), nameof(Hit.NLP), nameof(Hit.NVL), nameof(Hit.NTL),
        nameof(Hit.SHit), nameof(Hit.STE), nameof(Hit.SVE), nameof(Hit.SEP), nameof(Hit.SPM), nameof(Hit.SXP), nameof(Hit.SPP), nameof(Hit.SP), nameof(Hit.SLP), nameof(Hit.SVL), nameof(Hit.STL),
        nameof(Hit.CHit), nameof(Hit.CTE), nameof(Hit.CVE), nameof(Hit.CEP), nameof(Hit.CPM), nameof(Hit.CXP), nameof(Hit.CPP), nameof(Hit.CP), nameof(Hit.CLP), nameof(Hit.CVL), nameof(Hit.CTL),
        nameof(Hit.LT),   nameof(Hit.LV),  nameof(Hit.LELP),
        nameof(Hit.NT),   nameof(Hit.NV),  nameof(Hit.NELP),
        nameof(Hit.ST),   nameof(Hit.SV),  nameof(Hit.SELP),
        nameof(Hit.CT),   nameof(Hit.CV),  nameof(Hit.CELP),
        "LHitRaw", "NHitRaw", "SHitRaw", "CHitRaw",
        nameof(Hit.LFast), nameof(Hit.NFast), nameof(Hit.SFast), nameof(Hit.CFast),
        nameof(Hit.LSlow), nameof(Hit.NSlow), nameof(Hit.SSlow), nameof(Hit.CSlow)
    ])]
    public static class Hit__GetHitMarginInSec {
        public static bool Prefix(double timeDiff, float floorBpm, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return true;
            }
            ApplyHit(controller,
                Hit.GetHitMarginInSec(Difficulty.Lenient, timeDiff, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInSec(Difficulty.Normal, timeDiff, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInSec(Difficulty.Strict, timeDiff, floorBpm, conductorPitch, marginScale),
                ref __result);
            return false;
        }
    }
    #endregion

    #region ComboStats
    [LazyPatch("Tags.P_scrMisc.ComboStats__GetHitMarginInDeg", "scrMisc", "GetHitMarginInDeg", Triggers =
    [
        nameof(ComboStats.Combo), nameof(ComboStats.MaxCombo),
        nameof(ComboStats.LMarginCombo), nameof(ComboStats.NMarginCombo), nameof(ComboStats.SMarginCombo), nameof(ComboStats.MarginCombo),
        nameof(ComboStats.LMarginMaxCombo), nameof(ComboStats.NMarginMaxCombo), nameof(ComboStats.SMarginMaxCombo), nameof(ComboStats.MarginMaxCombo),
        nameof(ComboStats.LMarginCombos), nameof(ComboStats.NMarginCombos), nameof(ComboStats.SMarginCombos), nameof(ComboStats.MarginCombos),
        nameof(ComboStats.LMarginMaxCombos), nameof(ComboStats.NMarginMaxCombos), nameof(ComboStats.SMarginMaxCombos), nameof(ComboStats.MarginMaxCombos),
        nameof(ComboStats.SpecialPlayMark)
    ])]
    public static class Combo__GetHitMarginInDeg {
        public static void Postfix(float hitAngle, float refAngle, bool clockwise, float floorBpm, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyCombo(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                __result);
        }
    }

    [LazyPatch("Tags.P_scrMisc.ComboStats__GetHitMarginInSec", "scrMisc", "GetHitMarginInSec", Triggers =
    [
        nameof(ComboStats.Combo), nameof(ComboStats.MaxCombo),
        nameof(ComboStats.LMarginCombo), nameof(ComboStats.NMarginCombo), nameof(ComboStats.SMarginCombo), nameof(ComboStats.MarginCombo),
        nameof(ComboStats.LMarginMaxCombo), nameof(ComboStats.NMarginMaxCombo), nameof(ComboStats.SMarginMaxCombo), nameof(ComboStats.MarginMaxCombo),
        nameof(ComboStats.LMarginCombos), nameof(ComboStats.NMarginCombos), nameof(ComboStats.SMarginCombos), nameof(ComboStats.MarginCombos),
        nameof(ComboStats.LMarginMaxCombos), nameof(ComboStats.NMarginMaxCombos), nameof(ComboStats.SMarginMaxCombos), nameof(ComboStats.MarginMaxCombos),
        nameof(ComboStats.SpecialPlayMark)
    ])]
    public static class Combo__GetHitMarginInSec {
        public static void Postfix(double timeDiff, float floorBpm, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyCombo(controller,
                Hit.GetHitMarginInSec(Difficulty.Lenient, timeDiff, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInSec(Difficulty.Normal, timeDiff, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInSec(Difficulty.Strict, timeDiff, floorBpm, conductorPitch, marginScale),
                __result);
        }
    }
    #endregion

    #region Scores
    [LazyPatch("Tags.P_scrMisc.Scores__GetHitMarginInDeg", "scrMisc", "GetHitMarginInDeg", Triggers =
    [
        nameof(Scores.LScore), nameof(Scores.NScore), nameof(Scores.SScore), nameof(Scores.Score)
    ])]
    public static class Scores__GetHitMarginInDeg {
        public static void Postfix(float hitAngle, float refAngle, bool clockwise, float floorBpm, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyScores(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitAngle, refAngle, clockwise, floorBpm, conductorPitch, marginScale),
                __result);
        }
    }

    [LazyPatch("Tags.P_scrMisc.Scores__GetHitMarginInSec", "scrMisc", "GetHitMarginInSec", Triggers =
    [
        nameof(Scores.LScore), nameof(Scores.NScore), nameof(Scores.SScore), nameof(Scores.Score)
    ])]
    public static class Scores__GetHitMarginInSec {
        public static void Postfix(double timeDiff, float floorBpm, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyScores(controller,
                Hit.GetHitMarginInSec(Difficulty.Lenient, timeDiff, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInSec(Difficulty.Normal, timeDiff, floorBpm, conductorPitch, marginScale),
                Hit.GetHitMarginInSec(Difficulty.Strict, timeDiff, floorBpm, conductorPitch, marginScale),
                __result);
        }
    }
    #endregion
}
