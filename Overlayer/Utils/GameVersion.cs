using HarmonyLib;
using System.Linq;
using System.Reflection;

namespace Overlayer.Utils;

// LazyPatchAttribute used `typeof(Releases).GetField(...)` directly, which crashes at JIT time
// (TypeLoadException) the moment ANY code touching it runs, on a build where `Releases` doesn't
// exist (verified against older-2.9.8 during investigation). MiscUtils.TypeByName resolves by
// name and returns null instead of throwing, so this degrades gracefully on those builds.
//
// `Releases.releaseNumber` only exists from r147 onward; `general`/`older`/`older-2.9.8` still
// carry the pre-r147 const on `GCNS.releaseNumber` (confirmed: that's exactly what Releases
// replaced, pre-ca3c732). Without this fallback, every pre-r147 build reads -1 here, which then
// fails every MinVersion-gated LazyPatch as "Not Compatible" instead of detecting the real build.
public static class GameVersion {
    public static readonly int Current = GetReleaseNumber("Releases") ?? GetReleaseNumber("GCNS") ?? -1;

    private static int? GetReleaseNumber(string typeName) =>
        MiscUtils.TypeByName(typeName)?.GetField("releaseNumber")?.GetValue(null) is int v ? v : null;

    // PlanetarySystem as a TYPE already exists on older-2.9.8 (it holds chosenPlanet/planetList/etc
    // there too), so checking type-existence alone is NOT a safe proxy for "does
    // scrController.planetarySystem.speed/isCW work" - confirmed live: that false positive caused a
    // MissingMethodException storm (get_planetarySystem() not found) on every judgment. The real
    // boundary is r145 (ca3c732's scrPlayer split): from r145 on, `scrController.planetarySystem` is
    // an auto-property proxying `playerOne.planetarySystem`, and `speed`/`isCW` are plain fields on
    // that PlanetarySystem. On 2.9.8, `scrController.planetarySystem` is itself a plain field, and
    // speed/isCW never moved off scrController at all (confirmed via decompile: `public double
    // speed = 1.0;` / `public bool isCW = true;` directly on 2.9.8's scrController, PlanetarySystem
    // has no speed/isCW member there). So gate on HasScrPlayer (below), not type existence.
    // Same hazard as everywhere else: a single method touching both forms behind an `if` would get
    // JIT'd whole and throw TypeLoadException for PlanetarySystem on a build without it, so each
    // form is its own method and only the one actually called ever gets JIT'd.
    // Same r147 refactor, same hazard, for scrPlayer-gated members (consecMultipressCounter,
    // midspinInfiniteMargin, OnDamage, playerOne.failBar, playerOne.marginTracker, ...).
    public static readonly bool HasScrPlayer = MiscUtils.TypeByName("scrPlayer") != null;

    private static double GetSpeedViaPlanetarySystem(scrController ctrl) => ctrl.planetarySystem.speed;
    private static AccessTools.FieldRef<scrController, double> speedRef;
    private static double GetSpeedDirect(scrController ctrl) {
        speedRef ??= AccessTools.FieldRefAccess<scrController, double>("speed");
        return speedRef(ctrl);
    }
    public static double GetSpeed(scrController ctrl) =>
        HasScrPlayer ? GetSpeedViaPlanetarySystem(ctrl) : GetSpeedDirect(ctrl);

    private static bool GetIsCWViaPlanetarySystem(scrController ctrl) => ctrl.planetarySystem.isCW;
    private static AccessTools.FieldRef<scrController, bool> isCWRef;
    private static bool GetIsCWDirect(scrController ctrl) {
        isCWRef ??= AccessTools.FieldRefAccess<scrController, bool>("isCW");
        return isCWRef(ctrl);
    }
    public static bool GetIsCW(scrController ctrl) =>
        HasScrPlayer ? GetIsCWViaPlanetarySystem(ctrl) : GetIsCWDirect(ctrl);

    // scrPlanet.targetExitAngle is a plain public field on older-2.9.8 (`public double
    // targetExitAngle;`, confirmed via decompile) but an auto-property on the current build - a
    // direct `.targetExitAngle` read compiles to `get_targetExitAngle()`, which throws
    // MissingMethodException on 2.9.8 (confirmed live: every scrController.Hit() call, i.e. every
    // judgment, crashed through this inside the Harmony-patched scrPlanet.SwitchChosen). Resolved
    // by member kind instead of a hard property/field access, same as RDString.AvailableLanguages
    // and TMP_Asset.material above.
    private static MemberInfo targetExitAngleMember;
    public static double GetTargetExitAngle(scrPlanet planet) {
        targetExitAngleMember ??= typeof(scrPlanet).GetMember("targetExitAngle", BindingFlags.Public | BindingFlags.Instance).FirstOrDefault();
        return targetExitAngleMember switch {
            PropertyInfo p => (double)p.GetValue(planet),
            FieldInfo f => (double)f.GetValue(planet),
            _ => 0.0
        };
    }
}
