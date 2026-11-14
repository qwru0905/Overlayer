using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlayer.Tags;

// The OTHER direction from GameMargin: name <-> value for Overlayer's OWN classification
// (Hit.Lenient/Normal/Strict/Current, the MarginCombo slots, ...). Those always hold alpha's
// compiled-in literals, on every build - so converting them through the runtime HitMargin type
// (Enum.Parse / ToString / EnumHelper<HitMargin>) reads the LOADED build's numbering instead and
// mismatches on 145/148/136: there "LatePerfect" parses to 4 (Overlayer's XPerfect slot), and
// Overlayer's XPerfect (4) prints as "LatePerfect"; "XPerfect"/"PerfectMinus"/"PerfectPlus" don't
// parse at all. Both `nameof(HitMargin.X)` and `HitMargin.X` are compile-time constants, so this
// table is fixed to alpha and identical on every build.
public static class MarginName {
    private static readonly (string name, HitMargin value)[] Table = [
        (nameof(HitMargin.TooEarly), HitMargin.TooEarly),
        (nameof(HitMargin.VeryEarly), HitMargin.VeryEarly),
        (nameof(HitMargin.EarlyPerfect), HitMargin.EarlyPerfect),
        (nameof(HitMargin.PerfectMinus), HitMargin.PerfectMinus),
        (nameof(HitMargin.XPerfect), HitMargin.XPerfect),
        (nameof(HitMargin.PerfectPlus), HitMargin.PerfectPlus),
        (nameof(HitMargin.LatePerfect), HitMargin.LatePerfect),
        (nameof(HitMargin.VeryLate), HitMargin.VeryLate),
        (nameof(HitMargin.TooLate), HitMargin.TooLate),
        (nameof(HitMargin.Multipress), HitMargin.Multipress),
        (nameof(HitMargin.FailMiss), HitMargin.FailMiss),
        (nameof(HitMargin.FailOverload), HitMargin.FailOverload),
        (nameof(HitMargin.Auto), HitMargin.Auto),
        (nameof(HitMargin.OverPress), HitMargin.OverPress),
        (nameof(HitMargin.Midspin), HitMargin.Midspin),
        (nameof(HitMargin.FailedFloor), HitMargin.FailedFloor),
    ];

    // Keyed by the raw int, not HitMargin: on an older build several of these values aren't
    // defined members of the loaded enum at all.
    private static readonly Dictionary<string, HitMargin> ByName = Table.ToDictionary(t => t.name, t => t.value);
    private static readonly Dictionary<int, string> ByValue = Table.ToDictionary(t => (int)t.value, t => t.name);

    // One past the largest value, for arrays indexed by an Overlayer-classified HitMargin.
    public static readonly int SlotCount = Table.Max(t => (int)t.value) + 1;

    // Unknown names fall back to default(HitMargin) (TooEarly), like EnumHelper<T>.Parse did.
    public static HitMargin Parse(string name) => name != null && ByName.TryGetValue(name.Trim(), out var value) ? value : default;

    public static string NameOf(HitMargin margin) => ByValue.TryGetValue((int)margin, out var name) ? name : ((int)margin).ToString();

    public static HitMargin[] SplitParse(string names, char splitter) => Array.ConvertAll(names.Split(splitter), Parse);
}
