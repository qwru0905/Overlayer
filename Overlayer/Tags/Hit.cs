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
    public static string LHit(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => HitText(Lenient).Trim(maxLength, afterTrimStr);
    [Tag]
    [TagDesc("Normal Hit")]
    public static string NHit(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => HitText(Normal).Trim(maxLength, afterTrimStr);
    [Tag]
    [TagDesc("Strict Hit")]
    public static string SHit(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => HitText(Strict).Trim(maxLength, afterTrimStr);
    [Tag]
    [TagDesc("Current Hit")]
    public static string CHit(int maxLength = -1, string afterTrimStr = Extensions.DefaultTrimStr) => HitText(Current).Trim(maxLength, afterTrimStr);

    // "HitMargin." + margin.ToString() (the old code) asks the CLR to look up the value in the
    // RUNTIME-LOADED HitMargin type's own name table, not Overlayer's compiled intent - on 148,
    // value 4 is named "LatePerfect", so an XPerfect hit (Overlayer's own classification) would
    // display the "LatePerfect" localization string. NameOf maps by comparing against Overlayer's
    // OWN compiled literals (self-consistent - see the spec's "self-consistent" section), which is
    // safe, then HitText falls back from the 3-way split's key to the pre-150 "Perfect" key when
    // the split-specific key doesn't exist yet on this build's localization table.
    static string NameOf(HitMargin m) => m switch {
        HitMargin.TooEarly => nameof(HitMargin.TooEarly),
        HitMargin.VeryEarly => nameof(HitMargin.VeryEarly),
        HitMargin.EarlyPerfect => nameof(HitMargin.EarlyPerfect),
        HitMargin.PerfectMinus => nameof(HitMargin.PerfectMinus),
        HitMargin.XPerfect => nameof(HitMargin.XPerfect),
        HitMargin.PerfectPlus => nameof(HitMargin.PerfectPlus),
        HitMargin.LatePerfect => nameof(HitMargin.LatePerfect),
        HitMargin.VeryLate => nameof(HitMargin.VeryLate),
        HitMargin.TooLate => nameof(HitMargin.TooLate),
        HitMargin.FailMiss => nameof(HitMargin.FailMiss), // FixMargin's noFailInfiniteMargin branch can set this
        _ => m.ToString(), // shouldn't happen - Lenient/Normal/Strict/Current only ever hold one of the above - but degrade instead of throwing
    };

    static string HitText(HitMargin margin) {
        string text = RDString.GetWithCheck("HitMargin." + NameOf(margin), out bool exists);
        if (!exists && margin is HitMargin.PerfectMinus or HitMargin.XPerfect or HitMargin.PerfectPlus) {
            // Pre-150 localization has no PerfectMinus/XPerfect/PerfectPlus entries (that 3-way
            // split didn't exist yet), only a single "Perfect". "Perfect" isn't a member of the
            // HitMargin Overlayer compiles against (alpha), so this is a plain string, not nameof.
            text = RDString.Get("HitMargin.Perfect");
        }
        return text;
    }

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

    // GameMargin resolves "X" against whichever HitMargin type is actually loaded (see
    // GameMargin.cs), instead of (int)HitMargin.X, which is fixed to Overlayer's alpha-compiled
    // literal and can point at the wrong slot - or a member that doesn't exist at all - on an
    // older game build. Returns 0 when the member doesn't exist on the running build.
    static int GameCount(string name) => GameMargin.IndexOf(name) is int i ? hitMarginsCount?[i] ?? 0 : 0;

    [Tag]
    [TagDesc("Official Too Early")]
    public static int OTE => GameCount(nameof(HitMargin.TooEarly));
    [Tag]
    [TagDesc("Official Very Early")]
    public static int OVE => GameCount(nameof(HitMargin.VeryEarly));
    [Tag]
    [TagDesc("Official Early Perfect")]
    public static int OEP => GameCount(nameof(HitMargin.EarlyPerfect));
    [Tag]
    [TagDesc("Official Perfect")]
    public static int OP => OPP + OA;
    [Tag]
    [TagDesc("Official Late Perfect")]
    public static int OLP => GameCount(nameof(HitMargin.LatePerfect));
    [Tag]
    [TagDesc("Normal Very Late")]
    public static int OVL => GameCount(nameof(HitMargin.VeryLate));
    [Tag]
    [TagDesc("Official Too Late")]
    public static int OTL => GameCount(nameof(HitMargin.TooLate));
    [Tag]
    [TagDesc("Official Perfect (Only Auto)")]
    public static int OA => GameCount(nameof(HitMargin.Auto));
    [Tag]
    [TagDesc("Official Perfect Minus (early side of the perfect window)")]
    public static int OPM => GameCount(nameof(HitMargin.PerfectMinus));
    [Tag]
    [TagDesc("Official X Perfect (center of the perfect window)")]
    public static int OXP => GameCount(nameof(HitMargin.XPerfect));
    [Tag]
    [TagDesc("Official Perfect Plus (late side of the perfect window)")]
    public static int OPL => GameCount(nameof(HitMargin.PerfectPlus));
    [Tag]
    [TagDesc("Official Perfect (Only Player, Minus + X + Plus)")]
    // Pre-150 has no PerfectMinus/XPerfect/PerfectPlus split (OPM/OXP/OPL are all 0 there, per
    // GameCount's "member doesn't exist -> 0" contract) - fall back to that build's own single
    // "Perfect" slot instead of silently reporting 0 for what's actually a normal perfect count.
    public static int OPP => GameMargin.TryGet(nameof(HitMargin.XPerfect)) != null ? OPM + OXP + OPL : GameCount("Perfect");
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
    public static int MissCount => GameMargin.TryGet(nameof(HitMargin.FailMiss)) is HitMargin m ? scrController.instance?.playerOne?.marginTracker?.GetHits(m) ?? 0 : 0;
    [Tag]
    [TagDesc("Number of Overloads")]
    public static int Overloads => GameMargin.TryGet(nameof(HitMargin.FailOverload)) is HitMargin m ? scrController.instance?.playerOne?.marginTracker?.GetHits(m) ?? 0 : 0;
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

    // A level-design "Auto" tile (scrFloor.auto on the tile about to be entered) is a completely
    // different thing from the player's Autoplay toggle (RDC.auto), but scrPlayer.OttoHoldHit()
    // temporarily flips the GLOBAL RDC.auto to true while it processes an Auto tile, then flips it
    // back - so RDC.auto alone can't tell "the player turned Autoplay on" apart from "the game is
    // auto-advancing a level-design Auto tile". The tile's own .auto field (exposed here via
    // scrController.currFloor -> nextfloor, exactly how the real game's scrPlanet.SwitchChosen
    // reads it as `flag`) is the only reliable signal.
    public static bool IsAutoTile(scrController ctrl) => ctrl.gameworld && (ctrl.currFloor?.nextfloor?.auto ?? false);

    // Mirrors scrPlanet.SwitchChosen's own override chain on the real per-difficulty classification,
    // confirmed against the 150 decompile, in this exact precedence (each condition below can
    // overwrite the previous one):
    //   1. Plain Autoplay (RDC.auto, not on an Auto tile) && !useOldAuto -> a genuine, fully-weighted
    //      XPerfect. HitMarginHelper.PlayerHitMarginWeights/HitMarginXScores have a normal entry for
    //      XPerfect, so this counts fully - it's why the in-game accuracy display reads 100% during
    //      a plain Autoplay run, and callers must NOT skip it.
    //   2. An Auto tile (see IsAutoTile) -> HitMargin.Auto, unconditionally. This wins over #1
    //      (matches the real game: its own Auto-tile check has no !useOldAuto guard). Auto is the
    //      one classification HitMargin the weight tables have no entry for, so it genuinely earns
    //      zero score/accuracy weight.
    //   3. Midspin -> HitMargin.Midspin, wins over everything above.
    // Exposed as a pure function (no UnityEngine.Object touched) so the precedence itself is
    // unit-testable without a live game.
    public static HitMargin ApplyAutoOverride(HitMargin hitMargin, bool autoplayActive, bool useOldAuto, bool isAutoTile, bool midspin) {
        if(autoplayActive && !useOldAuto) {
            hitMargin = HitMargin.XPerfect;
        }
        if(isAutoTile) {
            hitMargin = HitMargin.Auto;
        }
        if(midspin) {
            hitMargin = HitMargin.Midspin;
        }
        return hitMargin;
    }

    // Only an Auto tile or Midspin hit is excluded from Overlayer's own counts (see
    // ApplyAutoOverride) - a plain Autoplay-cheat hit is a genuine XPerfect and should count
    // normally, matching the game's own accuracy display.
    public static bool IsAutoOrMidspin(scrController ctrl) =>
        ctrl.gameworld && (ctrl.playerOne.midspinInfiniteMargin || IsAutoTile(ctrl));

    public static void FixMargin(scrController ctrl, ref HitMargin hitMargin) {
        if(ctrl.gameworld) {
            if(ctrl.noFailInfiniteMargin) {
                hitMargin = HitMargin.FailMiss;
            }
            hitMargin = ApplyAutoOverride(hitMargin, RDC.auto, RDC.useOldAuto, IsAutoTile(ctrl), ctrl.playerOne.midspinInfiniteMargin);
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

    public static HitMargin GetHitMarginInDeg(Difficulty diff, float hitAngle, float refAngle, bool clockwise, float floorBpm, float conductorPitch, double marginScale) {
        float angleDeg = 57.29578f * (hitAngle - refAngle) * (clockwise ? 1 : -1);
        return SelectHitMargin(diff, angleDeg, floorBpm, conductorPitch, marginScale);
    }

    // Async-input path of the game: it hands us a time offset (seconds) instead of angles.
    // The game's time boundaries are the angle boundaries scaled by the same linear factor,
    // so converting the offset to degrees and reusing the angle boundaries gives the same result.
    public static HitMargin GetHitMarginInSec(Difficulty diff, double timeDiff, float floorBpm, float conductorPitch, double marginScale) {
        double angleDeg = scrMisc.TimeToAngleInRad(timeDiff, floorBpm, conductorPitch, false) * 57.295780181884766;
        return SelectHitMargin(diff, angleDeg, floorBpm, conductorPitch, marginScale);
    }

    private static HitMargin SelectHitMargin(Difficulty diff, double angleDeg, float bpmTimesSpeed, float conductorPitch, double marginScale) {
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
