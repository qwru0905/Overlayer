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

    private static void ApplyHitCommon(scrController controller, HitMargin l, HitMargin n, HitMargin s) {
        Hit.Lenient = l;
        Hit.Normal = n;
        Hit.Strict = s;
        Hit.FixMargin(controller, ref Hit.Lenient);
        Hit.FixMargin(controller, ref Hit.Normal);
        Hit.FixMargin(controller, ref Hit.Strict);
        Hit.Current = Hit.GetCHit(GCS.difficulty);
        // An autoplay/midspin hit is forced to XPerfect above so combo-continuation still sees
        // it as a perfect (matching the game's own PerfectHitMargins), but it isn't a genuine
        // XPerfect-timing hit, so it shouldn't inflate the XP/etc. counts (matching the game's
        // own PlayerHitMarginWeights/HitMarginXScores, which give it no weight/score either).
        if(!Hit.ControllerIsSafe(controller) && !Hit.IsAutoOrMidspin(controller)) {
            Hit.IncreaseCount(Difficulty.Lenient, Hit.Lenient);
            Hit.IncreaseCount(Difficulty.Normal, Hit.Normal);
            Hit.IncreaseCount(Difficulty.Strict, Hit.Strict);
            Hit.IncreaseCCount(Hit.Current);
        }
    }

    // 150+: Overlayer is compiled against alpha, so feeding the game back Overlayer's own
    // alpha-literal classification via __result is self-consistent (CurrentBuild == CompileTarget)
    // - the game reads the exact same numbering it was given.
    private static void ApplyHit(scrController controller, HitMargin l, HitMargin n, HitMargin s, ref HitMargin result) {
        ApplyHitCommon(controller, l, n, s);
        result = Hit.Current;
    }

    // Pre-150: must NOT write __result. The old scrMisc.GetHitMargin's caller passes its return
    // value to scrMisc.IsValidHit(hitMargin), which compares it against THIS BUILD's own HitMargin
    // numbering to decide whether the hit counts at all. Writing Overlayer's alpha-numbered
    // classification back made the game reject LatePerfect/VeryLate hits outright, and - under the
    // PurePerfectOnly/PerfectsOnly settings - even reject dead-center XPerfect/PerfectPlus hits
    // (confirmed against 148's real IsValidHit). Compute Overlayer's own values for internal
    // bookkeeping only, and leave __result exactly as the original game method set it.
    private static void ApplyHitReadOnly(scrController controller, HitMargin l, HitMargin n, HitMargin s) {
        ApplyHitCommon(controller, l, n, s);
    }

    private static void ApplyCombo(scrController controller, HitMargin l, HitMargin n, HitMargin s, HitMargin result) {
        if(Hit.ControllerIsSafe(controller)) {
            return;
        }
        // PerfectMinus/XPerfect/PerfectPlus all sit inside the old single "Perfect!" window
        // (game r150+ split it three ways), so any of them should keep Combo alive; XCombo is the
        // stricter dead-center-only variant. Auto-tile/Midspin hits are skipped entirely (see
        // ComboStats.UpdateCombo), not treated as a break.
        ComboStats.UpdateCombo(result, Hit.IsAutoOrMidspin(controller));
        Hit.FixMargin(controller, ref l);
        Hit.FixMargin(controller, ref n);
        Hit.FixMargin(controller, ref s);
        // See the comment in ApplyHit: an autoplay/midspin hit keeps the combo alive above, but
        // shouldn't add Score or move a MarginCombo streak (the game gives it no X-Score either).
        if(!Hit.IsAutoOrMidspin(controller)) {
            Scores.SetScores(l, n, s, result);
            ComboStats.Combos_Set(Difficulty.Lenient, l);
            ComboStats.Combos_Set(Difficulty.Normal, n);
            ComboStats.Combos_Set(Difficulty.Strict, s);
        }
        ComboStats.SetMarginCombos();
    }

    private static void ApplyScores(scrController controller, HitMargin l, HitMargin n, HitMargin s, HitMargin result) {
        if(Hit.ControllerIsSafe(controller) || Hit.IsAutoOrMidspin(controller)) {
            return;
        }
        Hit.FixMargin(controller, ref l);
        Hit.FixMargin(controller, ref n);
        Hit.FixMargin(controller, ref s);
        Scores.SetScores(l, n, s, result);
    }

    #region Hit
    [LazyPatch("Tags.P_scrMisc.Hit__GetHitMarginInDeg", "scrMisc", "GetHitMarginInDeg", MinVersion = 150, Triggers =
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
        nameof(Hit.LSlow), nameof(Hit.NSlow), nameof(Hit.SSlow), nameof(Hit.CSlow),
        // P_scrMistakeManager.ApplyVersionIndependentStats reads Hit.CTE/CVE/CEP/CP/CLP/CVL/CTL
        // (populated only by this patch's ApplyHitCommon) to compute these three - without them
        // here, a config that displays only {CAccuracy}/{CXAccuracy}/{CXScore} and no Hit.* tag
        // never triggers this patch, so those counters stay frozen at 0 and the stats read as 0.
        nameof(AccuracyStats.CAccuracy), nameof(AccuracyStats.CXAccuracy), nameof(Scores.CXScore)
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

    [LazyPatch("Tags.P_scrMisc.Hit__GetHitMarginInSec", "scrMisc", "GetHitMarginInSec", MinVersion = 150, Triggers =
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
        nameof(Hit.LSlow), nameof(Hit.NSlow), nameof(Hit.SSlow), nameof(Hit.CSlow),
        // P_scrMistakeManager.ApplyVersionIndependentStats reads Hit.CTE/CVE/CEP/CP/CLP/CVL/CTL
        // (populated only by this patch's ApplyHitCommon) to compute these three - without them
        // here, a config that displays only {CAccuracy}/{CXAccuracy}/{CXScore} and no Hit.* tag
        // never triggers this patch, so those counters stay frozen at 0 and the stats read as 0.
        nameof(AccuracyStats.CAccuracy), nameof(AccuracyStats.CXAccuracy), nameof(Scores.CXScore)
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

    [LazyPatch("Tags.P_scrMisc.Hit__GetHitMargin", "scrMisc", "GetHitMargin", MaxVersion = 149, Triggers =
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
        nameof(Hit.LSlow), nameof(Hit.NSlow), nameof(Hit.SSlow), nameof(Hit.CSlow),
        // P_scrMistakeManager.ApplyVersionIndependentStats reads Hit.CTE/CVE/CEP/CP/CLP/CVL/CTL
        // (populated only by this patch's ApplyHitCommon) to compute these three - without them
        // here, a config that displays only {CAccuracy}/{CXAccuracy}/{CXScore} and no Hit.* tag
        // never triggers this patch, so those counters stay frozen at 0 and the stats read as 0.
        nameof(AccuracyStats.CAccuracy), nameof(AccuracyStats.CXAccuracy), nameof(Scores.CXScore)
    ])]
    public static class Hit__GetHitMargin {
        public static void Postfix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyHitReadOnly(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale));
        }
    }
    #endregion

    #region ComboStats
    [LazyPatch("Tags.P_scrMisc.ComboStats__GetHitMarginInDeg", "scrMisc", "GetHitMarginInDeg", MinVersion = 150, Triggers =
    [
        nameof(ComboStats.Combo), nameof(ComboStats.MaxCombo), nameof(ComboStats.XCombo), nameof(ComboStats.MaxXCombo),
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

    [LazyPatch("Tags.P_scrMisc.ComboStats__GetHitMarginInSec", "scrMisc", "GetHitMarginInSec", MinVersion = 150, Triggers =
    [
        nameof(ComboStats.Combo), nameof(ComboStats.MaxCombo), nameof(ComboStats.XCombo), nameof(ComboStats.MaxXCombo),
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

    [LazyPatch("Tags.P_scrMisc.Combo__GetHitMargin", "scrMisc", "GetHitMargin", MaxVersion = 149, Triggers =
    [
        nameof(ComboStats.Combo), nameof(ComboStats.MaxCombo), nameof(ComboStats.XCombo), nameof(ComboStats.MaxXCombo),
        nameof(ComboStats.LMarginCombo), nameof(ComboStats.NMarginCombo), nameof(ComboStats.SMarginCombo), nameof(ComboStats.MarginCombo),
        nameof(ComboStats.LMarginMaxCombo), nameof(ComboStats.NMarginMaxCombo), nameof(ComboStats.SMarginMaxCombo), nameof(ComboStats.MarginMaxCombo),
        nameof(ComboStats.LMarginCombos), nameof(ComboStats.NMarginCombos), nameof(ComboStats.SMarginCombos), nameof(ComboStats.MarginCombos),
        nameof(ComboStats.LMarginMaxCombos), nameof(ComboStats.NMarginMaxCombos), nameof(ComboStats.SMarginMaxCombos), nameof(ComboStats.MarginMaxCombos),
        nameof(ComboStats.SpecialPlayMark)
    ])]
    public static class Combo__GetHitMargin {
        // See Hit__GetHitMargin: this patch doesn't write __result, so it holds the game's own
        // correctly-numbered value here - but ApplyCombo's "is this a perfect" check and
        // Scores.SetScores compare against Overlayer's own alpha-literal classification (the same
        // self-consistent switches every other patch uses), so compute the current-difficulty
        // value fresh instead of reading __result.
        public static void Postfix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyCombo(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(GCS.difficulty, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale));
        }
    }
    #endregion

    #region Scores
    [LazyPatch("Tags.P_scrMisc.Scores__GetHitMarginInDeg", "scrMisc", "GetHitMarginInDeg", MinVersion = 150, Triggers =
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

    [LazyPatch("Tags.P_scrMisc.Scores__GetHitMarginInSec", "scrMisc", "GetHitMarginInSec", MinVersion = 150, Triggers =
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

    [LazyPatch("Tags.P_scrMisc.Scores__GetHitMargin", "scrMisc", "GetHitMargin", MaxVersion = 149, Triggers =
    [
        nameof(Scores.LScore), nameof(Scores.NScore), nameof(Scores.SScore), nameof(Scores.Score)
    ])]
    public static class Scores__GetHitMargin {
        // See Combo__GetHitMargin: compute the current-difficulty value fresh instead of reading
        // __result (the game's own, differently-numbered value on this build).
        public static void Postfix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyScores(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(GCS.difficulty, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale));
        }
    }
    #endregion
}
