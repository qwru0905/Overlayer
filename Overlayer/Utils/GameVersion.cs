namespace Overlayer.Utils;

// LazyPatchAttribute used `typeof(Releases).GetField(...)` directly, which crashes at JIT time
// (TypeLoadException) the moment ANY code touching it runs, on a build where `Releases` doesn't
// exist (verified against older-2.9.8 during investigation). MiscUtils.TypeByName resolves by
// name and returns null instead of throwing, so this degrades to -1 there.
public static class GameVersion {
    public static readonly int Current = MiscUtils.TypeByName("Releases")?.GetField("releaseNumber")?.GetValue(null) is int v ? v : -1;
}
