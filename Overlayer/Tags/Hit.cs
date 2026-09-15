using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System;
using UnityEngine;

namespace Overlayer.Tags;

public static class Hit {
    [Tag("LHitRaw")]
    [TagDesc("Lenient Hit Raw")]
    public static HitMargin Lenient;
    [Tag("NHitRaw")]
    [TagDesc("Normal Hit Raw")]
    public static HitMargin Normal;
    [Tag("SHitRaw")]
    [TagDesc("Strict Hit Raw")]
    public static HitMargin Strict;
    [Tag("CHitRaw")]
    [TagDesc("Current Hit Raw")]
    public static HitMargin Current;
    [Tag]
    [TagDesc("Lenient Hit")]
    public static string LHit(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => RDString.Get("HitMargin." + Lenient).Trim(maxLength, afterTrimStr);
    [Tag]
    [TagDesc("Normal Hit")]
    public static string NHit(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => RDString.Get("HitMargin." + Normal).Trim(maxLength, afterTrimStr);
    [Tag]
    [TagDesc("Strict Hit")]
    public static string SHit(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => RDString.Get("HitMargin." + Strict).Trim(maxLength, afterTrimStr);
    [Tag]
    [TagDesc("Current Hit")]
    public static string CHit(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => RDString.Get("HitMargin." + Current).Trim(maxLength, afterTrimStr);

    // Lenient
    [Tag]
    [TagDesc("Lenient Too Late - Too Early")]
    public static int LTE;
    [Tag]
    [TagDesc("Lenient Very Early")]
    public static int LVE;
    [Tag]
    [TagDesc("Lenient Early Perfect")]
    public static int LEP;
    [Tag]
    [TagDesc("Lenient Perfect Minus (early side of the perfect window)")]
    public static int LPM;
    [Tag]
    [TagDesc("Lenient X Perfect (center of the perfect window)")]
    public static int LXP;
    [Tag]
    [TagDesc("Lenient Perfect Plus (late side of the perfect window)")]
    public static int LPP;
    [Tag]
    [TagDesc("Lenient Perfect (Minus + X + Plus)")]
    public static int LP => LPM + LXP + LPP;
    [Tag]
    [TagDesc("Lenient Late Perfect")]
    public static int LLP;
    [Tag]
    [TagDesc("Lenient Very Late")]
    public static int LVL;
    [Tag]
    [TagDesc("Lenient Too Late")]
    public static int LTL;

    // Normal
    [Tag]
    [TagDesc("Normal Too Late - Too Early")]
    public static int NTE;
    [Tag]
    [TagDesc("Normal Very Early")]
    public static int NVE;
    [Tag]
    [TagDesc("Normal Early Perfect")]
    public static int NEP;
    [Tag]
    [TagDesc("Normal Perfect Minus (early side of the perfect window)")]
    public static int NPM;
    [Tag]
    [TagDesc("Normal X Perfect (center of the perfect window)")]
    public static int NXP;
    [Tag]
    [TagDesc("Normal Perfect Plus (late side of the perfect window)")]
    public static int NPP;
    [Tag]
    [TagDesc("Normal Perfect (Minus + X + Plus)")]
    public static int NP => NPM + NXP + NPP;
    [Tag]
    [TagDesc("Normal Late Perfect")]
    public static int NLP;
    [Tag]
    [TagDesc("Normal Very Late")]
    public static int NVL;
    [Tag]
    [TagDesc("Normal Too Late")]
    public static int NTL;

    // Strict
    [Tag]
    [TagDesc("Strict Too Late - Too Early")]
    public static int STE;
    [Tag]
    [TagDesc("Strict Very Early")]
    public static int SVE;
    [Tag]
    [TagDesc("Strict Early Perfect")]
    public static int SEP;
    [Tag]
    [TagDesc("Strict Perfect Minus (early side of the perfect window)")]
    public static int SPM;
    [Tag]
    [TagDesc("Strict X Perfect (center of the perfect window)")]
    public static int SXP;
    [Tag]
    [TagDesc("Strict Perfect Plus (late side of the perfect window)")]
    public static int SPP;
    [Tag]
    [TagDesc("Strict Perfect (Minus + X + Plus)")]
    public static int SP => SPM + SXP + SPP;
    [Tag]
    [TagDesc("Strict Late Perfect")]
    public static int SLP;
    [Tag]
    [TagDesc("Strict Very Late")]
    public static int SVL;
    [Tag]
    [TagDesc("Strict Too Late")]
    public static int STL;

    // Current
    [Tag]
    [TagDesc("Current Too Late - Too Early")]
    public static int CTE;
    [Tag]
    [TagDesc("Current Very Early")]
    public static int CVE;
    [Tag]
    [TagDesc("Current Early Perfect")]
    public static int CEP;
    [Tag]
    [TagDesc("Current Perfect Minus (early side of the perfect window)")]
    public static int CPM;
    [Tag]
    [TagDesc("Current X Perfect (center of the perfect window)")]
    public static int CXP;
    [Tag]
    [TagDesc("Current Perfect Plus (late side of the perfect window)")]
    public static int CPP;
    [Tag]
    [TagDesc("Current Perfect (Minus + X + Plus)")]
    public static int CP => CPM + CXP + CPP;
    [Tag]
    [TagDesc("Current Late Perfect")]
    public static int CLP;
    [Tag]
    [TagDesc("Current Very Late")]
    public static int CVL;
    [Tag]
    [TagDesc("Current Too Late")]
    public static int CTL;

    [Tag]
    [TagDesc("Fast judgment in Lenient difficulty")]
    public static int LFast => LTE + LVE + LEP;
    [Tag]
    [TagDesc("Fast judgment in Normal difficulty")]
    public static int NFast => NTE + NVE + NEP;
    [Tag]
    [TagDesc("Fast judgment in Strict difficulty")]
    public static int SFast => STE + SVE + SEP;
    [Tag]
    [TagDesc("Fast judgment in Current difficulty")]
    public static int CFast => CTE + CVE + CEP;
    [Tag]
    [TagDesc("Slow judgment in Lenient difficulty")]
    public static int LSlow => LTL + LVL + LLP;
    [Tag]
    [TagDesc("Slow judgment in Normal difficulty")]
    public static int NSlow => NTL + NVL + NLP;
    [Tag]
    [TagDesc("Slow judgment in Strict difficulty")]
    public static int SSlow => STL + SVL + SLP;
    [Tag]
    [TagDesc("Slow judgment in Current difficulty")]
    public static int CSlow => CTL + CVL + CLP;
    [Tag]
    [TagDesc("Lenient Early Perfect + Lenient Late Perfect")]
    public static int LELP => LEP + LLP;
    [Tag]
    [TagDesc("Normal Early Perfect + Normal Late Perfect")]
    public static int NELP => NEP + NLP;
    [Tag]
    [TagDesc("Strict Early Perfect + Strict Late Perfect")]
    public static int SELP => SEP + SLP;
    [Tag]
    [TagDesc("Current Early Perfect + Late Perfect")]
    public static int CELP => CEP + CLP;
    [Tag]
    [TagDesc("Lenient Very Early + Lenient Very Late")]
    public static int LV => LVE + LVL;
    [Tag]
    [TagDesc("Normal Very Early + Normal Very Late")]
    public static int NV => NVE + NVL;
    [Tag]
    [TagDesc("Strict Very Early + Strict Very Late")]
    public static int SV => SVE + SVL;
    [Tag]
    [TagDesc("Current Very Early + Current Very Late")]
    public static int CV => CVE + CVL;
    [Tag]
    [TagDesc("Lenient Too Early + Lenient Too Late")]
    public static int LT => LTE + LTL;
    [Tag]
    [TagDesc("Normal Too Early + Normal Too Late")]
    public static int NT => NTE + NTL;
    [Tag]
    [TagDesc("Strict Too Early + Strict Too Late")]
    public static int ST => STE + STL;
    [Tag]
    [TagDesc("Current Too Early + Current Too Late")]
    public static int CT => CTE + CTL;
    static int[] hitMarginsCount => scrController.instance?.playerOne?.marginTracker?.hitMarginsCount;

    [Tag]
    [TagDesc("Official Too Early")]
    public static int OTE => hitMarginsCount?[(int)HitMargin.TooEarly] ?? 0;
    [Tag]
    [TagDesc("Official Very Early")]
    public static int OVE => hitMarginsCount?[(int)HitMargin.VeryEarly] ?? 0;
    [Tag]
    [TagDesc("Official Early Perfect")]
    public static int OEP => hitMarginsCount?[(int)HitMargin.EarlyPerfect] ?? 0;
    [Tag]
    [TagDesc("Official Perfect")]
    public static int OP => OPP + OA;
    [Tag]
    [TagDesc("Official Late Perfect")]
    public static int OLP => hitMarginsCount?[(int)HitMargin.LatePerfect] ?? 0;
    [Tag]
    [TagDesc("Normal Very Late")]
    public static int OVL => hitMarginsCount?[(int)HitMargin.VeryLate] ?? 0;
    [Tag]
    [TagDesc("Official Too Late")]
    public static int OTL => hitMarginsCount?[(int)HitMargin.TooLate] ?? 0;
    [Tag]
    [TagDesc("Official Perfect (Only Auto)")]
    public static int OA => hitMarginsCount?[(int)HitMargin.Auto] ?? 0;
    [Tag]
    [TagDesc("Official Perfect Minus (early side of the perfect window)")]
    public static int OPM => hitMarginsCount?[(int)HitMargin.PerfectMinus] ?? 0;
    [Tag]
    [TagDesc("Official X Perfect (center of the perfect window)")]
    public static int OXP => hitMarginsCount?[(int)HitMargin.XPerfect] ?? 0;
    [Tag]
    [TagDesc("Official Perfect Plus (late side of the perfect window)")]
    public static int OPL => hitMarginsCount?[(int)HitMargin.PerfectPlus] ?? 0;
    [Tag]
    [TagDesc("Official Perfect (Only Player, Minus + X + Plus)")]
    public static int OPP => OPM + OXP + OPL;
    [Tag]
    [TagDesc("Fast judgment in Official")]
    public static int OFast => OTE + OVE + OEP;
    [Tag]
    [TagDesc("Slow judgment in Official")]
    public static int OSlow => OTL + OVL + OLP;
    [Tag]
    [TagDesc("Official Early Perfect + Official Late Perfect")]
    public static int OELP => OEP + OLP;
    [Tag]
    [TagDesc("Official Very Early + Official Very Late")]
    public static int OV => OVE + OVL;
    [Tag]
    [TagDesc("Official Too Early + Official Too Late")]
    public static int OT => OTE + OTL;

    [Tag]
    [TagDesc("Number of Misses")]
    public static int MissCount => scrController.instance?.playerOne?.marginTracker?.GetHits(HitMargin.FailMiss) ?? 0;
    [Tag]
    [TagDesc("Number of Overloads")]
    public static int Overloads => scrController.instance?.playerOne?.marginTracker?.GetHits(HitMargin.FailOverload) ?? 0;
    [Tag]
    [TagDesc("MissCount + Overloads")]
    public static int Fail => MissCount + Overloads;
    [Tag]
    [TagDesc("Number of Multipresses")]
    public static int Multipress;

    [Tag]
    [TagDesc("Current Difficulty")]
    public static string Difficulty(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => RDString.Get("enum.Difficulty." + GCS.difficulty).Trim(maxLength, afterTrimStr);
    [Tag]
    [TagDesc("Fixed difficulty return value regardless of language setting")]
    public static string DifficultyRaw(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => GCS.difficulty.ToString().Trim(maxLength, afterTrimStr);

    public static bool ControllerIsSafe(scrController ctrl) => ctrl.currFloor?.isSafe ?? false;

    public static void FixMargin(scrController ctrl, ref HitMargin hitMargin) {
        if(ctrl.gameworld) {
            if(ctrl.noFailInfiniteMargin) {
                hitMargin = HitMargin.FailMiss;
            }
            if(ctrl.playerOne.midspinInfiniteMargin || (RDC.auto && !RDC.useOldAuto)) {
                hitMargin = HitMargin.XPerfect;
            }
        }
    }

    public static void IncreaseCount(Difficulty diff, HitMargin hit) {
        switch(hit) {
            case HitMargin.TooEarly:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LTE++;
                        break;
                    case global::Difficulty.Normal:
                        NTE++;
                        break;
                    case global::Difficulty.Strict:
                        STE++;
                        break;
                }
                break;
            case HitMargin.VeryEarly:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LVE++;
                        break;
                    case global::Difficulty.Normal:
                        NVE++;
                        break;
                    case global::Difficulty.Strict:
                        SVE++;
                        break;
                }
                break;
            case HitMargin.EarlyPerfect:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LEP++;
                        break;
                    case global::Difficulty.Normal:
                        NEP++;
                        break;
                    case global::Difficulty.Strict:
                        SEP++;
                        break;
                }
                break;
            case HitMargin.PerfectMinus:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LPM++;
                        break;
                    case global::Difficulty.Normal:
                        NPM++;
                        break;
                    case global::Difficulty.Strict:
                        SPM++;
                        break;
                }
                break;
            case HitMargin.XPerfect:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LXP++;
                        break;
                    case global::Difficulty.Normal:
                        NXP++;
                        break;
                    case global::Difficulty.Strict:
                        SXP++;
                        break;
                }
                break;
            case HitMargin.PerfectPlus:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LPP++;
                        break;
                    case global::Difficulty.Normal:
                        NPP++;
                        break;
                    case global::Difficulty.Strict:
                        SPP++;
                        break;
                }
                break;
            case HitMargin.LatePerfect:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LLP++;
                        break;
                    case global::Difficulty.Normal:
                        NLP++;
                        break;
                    case global::Difficulty.Strict:
                        SLP++;
                        break;
                }
                break;
            case HitMargin.VeryLate:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LVL++;
                        break;
                    case global::Difficulty.Normal:
                        NVL++;
                        break;
                    case global::Difficulty.Strict:
                        SVL++;
                        break;
                }
                break;
            case HitMargin.TooLate:
                switch(diff) {
                    case global::Difficulty.Lenient:
                        LTL++;
                        break;
                    case global::Difficulty.Normal:
                        NTL++;
                        break;
                    case global::Difficulty.Strict:
                        STL++;
                        break;
                }
                break;
        }
    }

    public static void IncreaseCCount(HitMargin hit) {
        switch(hit) {
            case HitMargin.TooEarly:
                CTE++;
                break;
            case HitMargin.VeryEarly:
                CVE++;
                break;
            case HitMargin.EarlyPerfect:
                CEP++;
                break;
            case HitMargin.PerfectMinus:
                CPM++;
                break;
            case HitMargin.XPerfect:
                CXP++;
                break;
            case HitMargin.PerfectPlus:
                CPP++;
                break;
            case HitMargin.LatePerfect:
                CLP++;
                break;
            case HitMargin.VeryLate:
                CVL++;
                break;
            case HitMargin.TooLate:
                CTL++;
                break;
        }
    }

    public static double GetAdjustedAngleBoundaryInDeg(Difficulty diff, HitMarginGeneral marginType, double bpmTimesSpeed, double conductorPitch, double marginMult = 1.0) {
        float num = 0.065f;
        switch(diff) {
            case global::Difficulty.Lenient:
                num = 0.091f;
                break;
            case global::Difficulty.Normal:
                num = 0.065f;
                break;
            case global::Difficulty.Strict:
                num = 0.04f;
                break;
        }
        bool isMobile = ADOBase.isMobile;
        num = isMobile ? 0.09f : (num / GCS.currentSpeedTrial);
        float num2 = isMobile ? 0.07f : (0.03f / GCS.currentSpeedTrial);
        float a = isMobile ? 0.05f : (0.02f / GCS.currentSpeedTrial);
        num = Mathf.Max(num, 0.025f);
        num2 = Mathf.Max(num2, 0.025f);
        double num3 = (double)Mathf.Max(a, 0.025f);
        double val = scrMisc.TimeToAngleInRad((double)num, bpmTimesSpeed, conductorPitch, false) * 57.295780181884766;
        double val2 = scrMisc.TimeToAngleInRad((double)num2, bpmTimesSpeed, conductorPitch, false) * 57.295780181884766;
        double val3 = scrMisc.TimeToAngleInRad(num3, bpmTimesSpeed, conductorPitch, false) * 57.295780181884766;
        // XPerfect's hard window is a fixed ~16.6667ms (one 60fps frame), the same at every difficulty.
        double val4 = scrMisc.TimeToAngleInRad(0.01666666753590107, bpmTimesSpeed, conductorPitch, false) * 57.295780181884766;
        double result = Math.Max(GCS.HITMARGIN_COUNTED * marginMult, val);
        double result2 = Math.Max(45.0 * marginMult, val2);
        double result3 = Math.Max(30.0 * marginMult, val3);
        double result4 = Math.Max(12.5 * marginMult, val4);
        return marginType switch {
            HitMarginGeneral.Counted => result,
            HitMarginGeneral.Perfect => result2,
            HitMarginGeneral.Pure => result3,
            HitMarginGeneral.XPerfect => result4,
            _ => result,
        };
    }

    public static HitMargin GetHitMargin(Difficulty diff, float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale) {
        float angleDeg = 57.29578f * (hitangle - refangle) * (isCW ? 1 : -1);

        double countedDeg = GetAdjustedAngleBoundaryInDeg(diff, HitMarginGeneral.Counted, bpmTimesSpeed, conductorPitch, marginScale);
        double perfectDeg = GetAdjustedAngleBoundaryInDeg(diff, HitMarginGeneral.Perfect, bpmTimesSpeed, conductorPitch, marginScale);
        double pureDeg = GetAdjustedAngleBoundaryInDeg(diff, HitMarginGeneral.Pure, bpmTimesSpeed, conductorPitch, marginScale);
        double xperfectDeg = GetAdjustedAngleBoundaryInDeg(diff, HitMarginGeneral.XPerfect, bpmTimesSpeed, conductorPitch, marginScale);

        return angleDeg < -countedDeg
            ? HitMargin.TooEarly
            : angleDeg < -perfectDeg
            ? HitMargin.VeryEarly
            : angleDeg < -pureDeg
            ? HitMargin.EarlyPerfect
            : angleDeg < -xperfectDeg
            ? HitMargin.PerfectMinus
            : angleDeg <= xperfectDeg
            ? HitMargin.XPerfect
            : angleDeg <= pureDeg
            ? HitMargin.PerfectPlus
            : angleDeg <= perfectDeg ? HitMargin.LatePerfect : angleDeg <= countedDeg ? HitMargin.VeryLate : HitMargin.TooLate;
    }

    public static void Reset() {
        Lenient = Normal = Strict = Current = HitMargin.XPerfect;
        LTE = LVE = LEP = LPM = LXP = LPP = LLP = LVL = LTL = 0;
        NTE = NVE = NEP = NPM = NXP = NPP = NLP = NVL = NTL = 0;
        STE = SVE = SEP = SPM = SXP = SPP = SLP = SVL = STL = 0;
        CTE = CVE = CEP = CPM = CXP = CPP = CLP = CVL = CTL = 0;
        Multipress = 0;
    }

    public static HitMargin GetCHit(Difficulty diff) {
        return diff switch {
            global::Difficulty.Lenient => Lenient,
            global::Difficulty.Normal => Normal,
            global::Difficulty.Strict => Strict,
            _ => Strict,
        };
    }

    public static int GetHitCount(Difficulty diff, HitMargin margin) {
        return diff switch {
            global::Difficulty.Lenient => margin switch {
                HitMargin.TooEarly => LTE,
                HitMargin.VeryEarly => LVE,
                HitMargin.EarlyPerfect => LEP,
                HitMargin.PerfectMinus => LPM,
                HitMargin.XPerfect => LXP,
                HitMargin.PerfectPlus => LPP,
                HitMargin.LatePerfect => LLP,
                HitMargin.VeryLate => LVL,
                HitMargin.TooLate => LTL,
                _ => 0,
            },
            global::Difficulty.Normal => margin switch {
                HitMargin.TooEarly => NTE,
                HitMargin.VeryEarly => NVE,
                HitMargin.EarlyPerfect => NEP,
                HitMargin.PerfectMinus => NPM,
                HitMargin.XPerfect => NXP,
                HitMargin.PerfectPlus => NPP,
                HitMargin.LatePerfect => NLP,
                HitMargin.VeryLate => NVL,
                HitMargin.TooLate => NTL,
                _ => 0,
            },
            global::Difficulty.Strict => margin switch {
                HitMargin.TooEarly => STE,
                HitMargin.VeryEarly => SVE,
                HitMargin.EarlyPerfect => SEP,
                HitMargin.PerfectMinus => SPM,
                HitMargin.XPerfect => SXP,
                HitMargin.PerfectPlus => SPP,
                HitMargin.LatePerfect => SLP,
                HitMargin.VeryLate => SVL,
                HitMargin.TooLate => STL,
                _ => 0,
            },
            _ => 0,
        };
    }
}
