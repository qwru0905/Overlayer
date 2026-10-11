using HarmonyLib;
using Overlayer.Core.Patches;

namespace Overlayer.Tags.Patches;

public class P_scrController : PatchBase<P_scrController> {
    [LazyPatch("Tags.P_scrController.HitTiming__Awake_Rewind", "scrController", "Awake_Rewind", Triggers =
    [
        nameof(HitTiming.Timing), nameof(HitTiming.TimingAvg)
    ])]
    public static class HitTiming__Awake_Rewind {
        public static void Postfix() {
            HitTiming.Timing = 0;
            HitTiming.Timings = [];
        }
    }

    [LazyPatch("Tags.P_scrController.ProgressStats__FailAction", "scrController", "FailAction", Triggers =
    [
        nameof(ProgressStats.BestProgress)
    ])]
    public static class ProgressStats__FailAction {
        public static void Postfix() => ProgressStats.BestProgress_Update();
    }

    // OnDamage/consecMultipressCounter live on scrPlayer from the r147 refactor onward (alpha,
    // general r148, older r145). Two separate LazyPatches below cover both sides: only the one
    // whose target type actually exists on the running build resolves and patches - LazyPatch.
    // Patch() is a no-op when target is null, so having both declared is safe on every build.
    [LazyPatch("Tags.P_scrController.Hit__OnDamage", "scrPlayer", "OnDamage", Triggers =
    [
        nameof(Hit.Multipress)
    ])]
    public static class Hit__OnDamage {
        // __instance is `object`, not `scrPlayer`, on purpose: a typed parameter bakes `scrPlayer`
        // into this method's signature, and reflecting over this class's methods (TagManager scans
        // every type for [Tag] members) resolves that signature and throws TypeLoadException on a
        // build where scrPlayer doesn't exist at all (older-2.9.8) - even though target resolution
        // above already handles that build gracefully. The cast below only runs if this Postfix is
        // actually invoked, which only happens once Harmony has patched scrPlayer.OnDamage, which
        // can't happen on a build without scrPlayer.
        public static void Postfix(object __instance, bool multipress, bool applyMultipressDamage) {
            if(multipress) {
                if(applyMultipressDamage || ((scrPlayer)__instance).consecMultipressCounter > 5) {
                    Hit.Multipress++;
                }
            }
        }
    }

    // Pre-refactor (older-2.9.8/r136): OnDamage and consecMultipressCounter are still directly on
    // scrController (confirmed via decompile) - this is what Overlayer patched before ca3c732.
    // scrController always exists, so a typed __instance parameter here is safe on every build -
    // but the current build's scrController doesn't have consecMultipressCounter anymore (moved to
    // scrPlayer), so a direct __instance.consecMultipressCounter wouldn't even compile against it.
    // AccessTools.FieldRefAccess resolves the field by name at runtime instead, against whatever
    // scrController actually looks like on the build this is running on. general/older (r145/r148)
    // no longer have scrController.OnDamage at all, so this target resolves to null there and
    // LazyPatch.Patch() skips it - no double counting.
    [LazyPatch("Tags.P_scrController.Hit__OnDamage.Legacy", "scrController", "OnDamage", Triggers =
    [
        nameof(Hit.Multipress)
    ])]
    public static class Hit__OnDamage_Legacy {
        private static AccessTools.FieldRef<scrController, int> consecMultipressCounterRef;
        public static void Postfix(scrController __instance, bool multipress, bool applyMultipressDamage) {
            if(multipress) {
                consecMultipressCounterRef ??= AccessTools.FieldRefAccess<scrController, int>("consecMultipressCounter");
                if(applyMultipressDamage || consecMultipressCounterRef(__instance) > 5) {
                    Hit.Multipress++;
                }
            }
        }
    }

    [LazyPatch("Tags.P_scrController.ProgressStats__OnLandOnPortal", "scrController", "OnLandOnPortal", Triggers =
    [
        nameof(ProgressStats.BestProgress)
    ])]
    public static class ProgressStats__OnLandOnPortal {
        public static void Postfix() => ProgressStats.BestProgress_Fix();
    }
}
