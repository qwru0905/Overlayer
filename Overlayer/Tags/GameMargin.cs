using System;
using System.Collections.Generic;
using System.Linq;

namespace Overlayer.Tags;

// Resolves a HitMargin member by NAME against whichever HitMargin type is actually loaded.
// `HitMargin.X` in C# source is a compile-time literal fixed to whatever value Overlayer's own
// compiled reference (the alpha game assembly) had for X - looking it up by name at runtime
// instead reads the numbering the CURRENTLY LOADED game build actually uses, and returns null
// for a name that build doesn't have at all (e.g. PerfectMinus/XPerfect/PerfectPlus/Midspin/
// FailedFloor pre-150) instead of guessing a wrong slot.
public static class GameMargin {
    private static readonly Dictionary<string, HitMargin?> cache = new();

    public static HitMargin? TryGet(string name) {
        if (cache.TryGetValue(name, out var cached)) {
            return cached;
        }
        // Enum.TryParse(Type, string, out object) doesn't exist on the net4.8 API surface this
        // project targets (added in .NET Core 3.0) - Enum.Parse(Type, string) has been available
        // since .NET 1.0, so catch its ArgumentException instead for the "name doesn't exist" case.
        HitMargin? result;
        try {
            result = (HitMargin)Enum.Parse(typeof(HitMargin), name);
        } catch (ArgumentException) {
            result = null;
        }
        cache[name] = result;
        return result;
    }

    public static int? IndexOf(string name) => TryGet(name) is HitMargin m ? (int)m : null;

    public static HitMargin[] Existing(params string[] names) =>
        names.Select(TryGet).Where(m => m.HasValue).Select(m => m!.Value).ToArray();
}
