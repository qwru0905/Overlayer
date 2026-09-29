# Multi-Version Support (r145/r148/r150) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the single `Overlayer.dll` work correctly on ADOFAI release 150 (alpha, current target), 148 (`general`, the public Steam branch) and 145 (`older`), instead of only 150.

**Architecture:** The game's `HitMargin` enum has different members at different numeric values across these three builds, and C# bakes an enum member reference into IL as a literal int at compile time (Overlayer is compiled against alpha). Two small helpers absorb the difference: `GameVersion.Current` (the game's release number, resolved defensively) and `GameMargin` (resolves a `HitMargin` member **by name** against whichever type is actually loaded, instead of using Overlayer's compiled literal). Everywhere Overlayer crosses from its own self-computed values into the game's own arrays/methods/localization, it goes through one of these two helpers instead of a raw `HitMargin.X` literal or `.ToString()`. A few call sites reference game members (`xScore`, `maxXScore`, `maxPossibleXAcc`) that don't exist at all pre-150; those move into their own method that is only *called* on 150+, because Mono resolves a method's IL as a whole the first time it is invoked, so an `if` guard inside the same method is not enough to protect a reference to a nonexistent member.

**Tech Stack:** C# / .NET (targets `net481`-equivalent via Mono reference assemblies), HarmonyLib patches, Unity/ADOFAI's Mono runtime. No test framework exists in this repo; verification uses small standalone `mcs`/`mono` scratch console programs that load the real game assemblies, matching how every fix earlier in this project was verified (see [[lazypatch-validation]] in memory).

**Spec:** `docs/superpowers/specs/2026-09-29-multi-version-support-design.md`

## Global Constraints

- Support exactly release 150 (alpha, currently installed), 148 (`general`), and 145 (`older`). `older-2.9.8` (release 136) stays explicitly unsupported — no crash is required beyond what falls out naturally, but no feature work for it.
- No behavior change on 150 for anything not explicitly listed in a task below. Existing tags keep their exact current values on alpha.
- No `Overlayer.dll` other than the one already built from this project. One DLL must run correctly on all three in-scope builds; no per-version build/zip.
- New tags follow the existing `[Tag]`/`[TagDesc]` pattern already used by every sibling tag in the same file.
- A member that doesn't exist on the currently loaded game build must never throw. Resolve it by name at runtime (`GameMargin`) or read it from Overlayer's own self-computed counters instead of guessing a slot.
- A method must never contain a source-level reference (a plain member-access expression, not a `nameof`/string) to a game member that is absent from any in-scope build, even behind an `if` — Mono resolves a method's whole body the first time it is called, so the guard does not help (see spec, "why this is not just add a version check").

## Review Focus

- **A hit while not in a real level (freeroam, menus, replay/editor) on 148/145.** Every new/changed tag must still return its existing safe default (`0`, `""`, `double.NaN`) instead of throwing, exactly like the current alpha-only code already does via `?.`/`??`. Task 3's and Task 7's tests exercise `scrController.instance == null`/`playerOne == null` inputs explicitly.
- **A build whose release number is unrecognized (0, a very large number, or another mid-range number between 148 and 150 that doesn't exist yet).** `GameVersion`/`GameMargin`/the `MinVersion`/`MaxVersion` gates must degrale gracefully (treat as "closest known"), not throw or silently do nothing. Task 1's tests cover this with a fabricated `Releases.releaseNumber`.
- **A hit margin whose game-side name doesn't exist at all on the running build** (`PerfectMinus`/`XPerfect`/`PerfectPlus`/`Midspin`/`FailedFloor` on 148/145) reaching a raw array index or a `GetHits` call — must produce `0`, never an `IndexOutOfRangeException`. Task 3's and Task 6's tests run the exact 148/145 assemblies and assert no exception.
- **Every one of the 46 existing `LazyPatch` entries still resolves with zero validator issues on 148 and 145 after Task 5**, since Task 5 changes which patch classes apply to which version — a naming or `MinVersion`/`MaxVersion` mistake here would silently stop counting hits on one of the three builds. Task 5's test runs the full `LazyPatchValidator` harness against all three assemblies.
- **`CAccuracy`/`CXAccuracy` must actually match alpha's own `XAccuracy` bit-for-bit when run against the alpha assembly** — otherwise the copied weight table has a transcription error that would only show up as "slightly wrong numbers," not a crash. Task 7's test computes both from the same scripted hit sequence and asserts equality.

---

## Verification harness setup (used by every task below)

Every task's test step compiles a small, throwaway C# console program with `mcs` and runs it with `mono`, loading the just-built `Overlayer.dll` together with a real game `Assembly-CSharp.dll`. This mirrors the exact approach used earlier in this project (see [[lazypatch-validation]] in memory) and is **not** committed to the repo — it's a local verification step, created fresh in a scratch directory.

**Game assemblies available locally** (already downloaded before this plan was written):
- alpha (150, current target): `/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed/Assembly-CSharp.dll`
- 148 (`general`): `~/adofai-versions/general/A Dance of Fire and Ice_Data/Managed/Assembly-CSharp.dll`
- 145 (`older`): `~/adofai-versions/older/A Dance of Fire and Ice_Data/Managed/Assembly-CSharp.dll`

If these paths don't exist when executing this plan, re-download with:
```bash
DD=~/.local/share/DepotDownloader/DepotDownloader
F=~/adofai-versions/filelist.txt   # regex:^A Dance of Fire and Ice_Data/Managed/Assembly-CSharp(-firstpass)?\.dll$
"$DD" -app 977950 -os windows -beta older -username <steam id> -remember-password -dir ~/adofai-versions/older -filelist "$F"
"$DD" -app 977950 -os windows -dir ~/adofai-versions/general -filelist "$F"   # no -beta: public branch
```
(interactive login required — the user runs this, not the plan executor, if the files are missing)

Each task gives the exact harness source and command. Build the mod first:
```bash
cd /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer
dotnet build Overlayer/Overlayer.csproj -c Release --nologo
```
Expected: `오류 0개` (0 errors) in the output.

---

### Task 1: `GameVersion` — shared, crash-proof release-number lookup

**Files:**
- Create: `Overlayer/Utils/GameVersion.cs`
- Modify: `Overlayer/Core/Patches/LazyPatchAttribute.cs:10`
- Modify: `Overlayer/Overlayer.csproj` (register the new file)

**Interfaces:**
- Produces: `Overlayer.Utils.GameVersion.Current` (`int`, static readonly) — the game's `Releases.releaseNumber`, or `-1` if the `Releases` type doesn't exist on the loaded game assembly at all.

- [ ] **Step 1: Write the failing verification harness**

Create `/tmp/mvtest/Task1.cs`:
```csharp
using System;
using System.IO;
using System.Reflection;

static class Task1 {
    static int Main(string[] args) {
        var managed = args[0];
        var overlayerDll = args[1];
        var expected = args.Length > 2 ? int.Parse(args[2]) : (int?)null;
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        if (managed != "NONE") {
            Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        }
        var ov = Assembly.LoadFrom(overlayerDll);
        var gv = ov.GetType("Overlayer.Utils.GameVersion");
        if (gv == null) { Console.WriteLine("FAIL: GameVersion type not found"); return 1; }
        var current = (int)gv.GetField("Current").GetValue(null);
        Console.WriteLine("GameVersion.Current = " + current);
        if (expected.HasValue && current != expected.Value) {
            Console.WriteLine($"FAIL: expected {expected.Value}, got {current}");
            return 1;
        }
        Console.WriteLine("PASS");
        return 0;
    }
}
```

- [ ] **Step 2: Run it to verify it fails (type doesn't exist yet)**

```bash
mkdir -p /tmp/mvtest
mcs /tmp/mvtest/Task1.cs -out:/tmp/mvtest/Task1.exe
mono /tmp/mvtest/Task1.exe "/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer/Overlayer/bin/Release/Overlayer.dll 150
```
Expected: `FAIL: GameVersion type not found`.

- [ ] **Step 3: Create `GameVersion.cs`**

```csharp
using Overlayer.Utils;

namespace Overlayer.Utils;

// LazyPatchAttribute used `typeof(Releases).GetField(...)` directly, which crashes at JIT time
// (TypeLoadException) the moment ANY code touching it runs, on a build where `Releases` doesn't
// exist (verified against older-2.9.8 during investigation). MiscUtils.TypeByName resolves by
// name and returns null instead of throwing, so this degrades to -1 there.
public static class GameVersion {
    public static readonly int Current = MiscUtils.TypeByName("Releases")?.GetField("releaseNumber")?.GetValue(null) is int v ? v : -1;
}
```

(Remove the stray `using Overlayer.Utils;` self-import line above — `GameVersion.cs` is itself in `Overlayer.Utils`, so it doesn't need to import itself. The file should start directly with `namespace Overlayer.Utils;`.)

- [ ] **Step 4: Register the file in the csproj**

Edit `Overlayer/Overlayer.csproj`, immediately after the line `<Compile Include="Utils\MiscUtils.cs" />` (around line 642), add:
```xml
    <Compile Include="Utils\GameVersion.cs" />
```

- [ ] **Step 5: Point `LazyPatchAttribute` at it**

In `Overlayer/Core/Patches/LazyPatchAttribute.cs`, replace line 10:
```csharp
    public static readonly int CurrentVersion = (int)typeof(Releases).GetField("releaseNumber").GetValue(null);
```
with:
```csharp
    public static readonly int CurrentVersion = GameVersion.Current;
```
(`using Overlayer.Utils;` is already present at the top of this file — no new `using` needed.)

- [ ] **Step 6: Build and run the harness again — verify it passes against all three in-scope builds**

```bash
dotnet build Overlayer/Overlayer.csproj -c Release --nologo
mono /tmp/mvtest/Task1.exe "/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" Overlayer/bin/Release/Overlayer.dll 150
mono /tmp/mvtest/Task1.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll 148
mono /tmp/mvtest/Task1.exe "$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll 145
```
Expected: `PASS` for all three.

- [ ] **Step 7: Verify the -1 fallback (Review Focus: unrecognized build)**

There is no locally available assembly without a `Releases` type to test this against directly (`older-2.9.8`, release 136, is out of scope and its DLL isn't part of this plan's setup). Instead, confirm the fallback logic itself with a tiny standalone check — compile and run this separately:
```csharp
// /tmp/mvtest/Task1Fallback.cs
using System;
class T {
    static void Main() {
        Type t = null; // simulates MiscUtils.TypeByName("Releases") returning null
        int current = t?.GetField("releaseNumber")?.GetValue(null) is int v ? v : -1;
        Console.WriteLine(current == -1 ? "PASS" : "FAIL: " + current);
    }
}
```
```bash
mcs /tmp/mvtest/Task1Fallback.cs -out:/tmp/mvtest/Task1Fallback.exe
mono /tmp/mvtest/Task1Fallback.exe
```
Expected: `PASS`.

- [ ] **Step 8: Run the full existing `LazyPatchValidator` harness to confirm no regression**

If a validator harness from a previous session still exists at `/tmp/*/scratchpad/harness/Harness.exe` (see [[lazypatch-validation]] in memory), reuse it; otherwise recreate it per that memory note. Run it against all three assemblies:
```bash
H=<path to Harness.exe>
mono "$H" "/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" Overlayer/bin/Release/Overlayer.dll neg
```
Expected: `46 patches checked, 0 with issues` and `ALL OK` (the 148/145 runs will still show the 2 pre-existing false positives from the old/new `GetHitMargin` split — that's fixed in Task 5, not here).

- [ ] **Step 9: Commit**

```bash
cd /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer
git add Overlayer/Utils/GameVersion.cs Overlayer/Core/Patches/LazyPatchAttribute.cs Overlayer/Overlayer.csproj
git commit -m "Extract GameVersion, resolved by name instead of a direct type reference

typeof(Releases) crashes at JIT time on a build where Releases doesn't
exist at all, even in a static field initializer that never runs on that
build (confirmed against older-2.9.8 during investigation). Route through
MiscUtils.TypeByName, which returns null instead of throwing, so this
degrades to -1.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 2: `GameMargin` — name-based `HitMargin` resolution

**Files:**
- Create: `Overlayer/Tags/GameMargin.cs`
- Modify: `Overlayer/Overlayer.csproj` (register the new file)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `Overlayer.Tags.GameMargin.TryGet(string name)` → `HitMargin?`; `GameMargin.IndexOf(string name)` → `int?`; `GameMargin.Existing(params string[] names)` → `HitMargin[]`. Tasks 3 and 6 call all three.

- [ ] **Step 1: Write the failing verification harness**

Create `/tmp/mvtest/Task2.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using System.Reflection;

static class Task2 {
    static object Call(Type t, string method, params object[] args) =>
        t.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

    static int Main(string[] args) {
        var managed = args[0];
        var overlayerDll = args[1];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var ov = Assembly.LoadFrom(overlayerDll);
        var gm = ov.GetType("Overlayer.Tags.GameMargin");
        if (gm == null) { Console.WriteLine("FAIL: GameMargin type not found"); return 1; }

        int fails = 0;
        void Expect(string label, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) fails++; }

        // TooEarly exists (and is at value 0) on every in-scope build.
        var tooEarly = Call(gm, "TryGet", "TooEarly");
        Expect("TooEarly resolves", tooEarly != null);

        // NoSuchMargin never exists anywhere.
        var missing = Call(gm, "TryGet", "NoSuchMargin");
        Expect("unknown name resolves to null", missing == null);

        var idx = (int?)Call(gm, "IndexOf", "TooEarly");
        Expect("IndexOf(TooEarly) == 0", idx == 0);
        var idxMissing = (int?)Call(gm, "IndexOf", "NoSuchMargin");
        Expect("IndexOf(unknown) == null", idxMissing == null);

        var existing = (Array)Call(gm, "Existing", (object)new[] { "TooEarly", "NoSuchMargin", "VeryEarly" });
        Expect("Existing(...) drops the unknown name, keeps the other two", existing.Length == 2);

        Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILED");
        return fails;
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
mcs /tmp/mvtest/Task2.cs -out:/tmp/mvtest/Task2.exe
mono /tmp/mvtest/Task2.exe "/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer/Overlayer/bin/Release/Overlayer.dll
```
Expected: `FAIL: GameMargin type not found`.

- [ ] **Step 3: Create `GameMargin.cs`**

```csharp
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
        HitMargin? result = Enum.TryParse(typeof(HitMargin), name, out var value) ? (HitMargin)value : null;
        cache[name] = result;
        return result;
    }

    public static int? IndexOf(string name) => TryGet(name) is HitMargin m ? (int)m : null;

    public static HitMargin[] Existing(params string[] names) =>
        names.Select(TryGet).Where(m => m.HasValue).Select(m => m!.Value).ToArray();
}
```

- [ ] **Step 4: Register the file in the csproj**

Edit `Overlayer/Overlayer.csproj`, immediately after the line `<Compile Include="Tags\Hit.cs" />` (around line 602), add:
```xml
    <Compile Include="Tags\GameMargin.cs" />
```

- [ ] **Step 5: Build and run the harness again — verify it passes on all three builds**

```bash
dotnet build Overlayer/Overlayer.csproj -c Release --nologo
for M in "/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" "$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed"; do
  mono /tmp/mvtest/Task2.exe "$M" Overlayer/bin/Release/Overlayer.dll
done
```
Expected: `ALL PASS` for all three (this specific test doesn't touch a name that differs by build — Task 3/6 exercise the version-dependent cases).

- [ ] **Step 6: Commit**

```bash
git add Overlayer/Tags/GameMargin.cs Overlayer/Overlayer.csproj
git commit -m "Add GameMargin: resolve a HitMargin member by name, not by literal

Every place Overlayer needs 'whatever value THIS build's HitMargin.X
actually is' (as opposed to comparing two of Overlayer's own compiled
literals, which is always safe) goes through this instead of a raw
HitMargin.X cast or GetHits(HitMargin.X, ...) call.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 3: `Hit.cs` — game-reported counts via `GameMargin` (`OTE`..`OPL`, `MissCount`, `Overloads`)

**Files:**
- Modify: `Overlayer/Tags/Hit.cs:222-256` (the `hitMarginsCount` block) and `:277-281` (`MissCount`/`Overloads`)

**Interfaces:**
- Consumes: `GameMargin.IndexOf(string)` (Task 2), `GameMargin.TryGet(string)` (Task 2).
- Produces: no change to any tag's public name or type — same `OTE`, `OVE`, `OEP`, `OLP`, `OVL`, `OTL`, `OA`, `OPM`, `OXP`, `OPL`, `MissCount`, `Overloads` signatures, just correct on every build.

- [ ] **Step 1: Write the failing verification harness**

Create `/tmp/mvtest/Task3.cs`. It loads Overlayer + a game assembly, uses reflection to read each tag property, and checks the *shape* of the result rather than a specific game-state value (there's no live game running, so `scrController.instance` is always null in this harness — that's the "no game running" input from Review Focus, which every one of these tags already has to handle via `?.`/`??`):
```csharp
using System;
using System.IO;
using System.Reflection;

static class Task3 {
    static int Main(string[] args) {
        var managed = args[0];
        var overlayerDll = args[1];
        var buildLabel = args[2]; // "150", "148", or "145" - informational only
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var ov = Assembly.LoadFrom(overlayerDll);
        var hit = ov.GetType("Overlayer.Tags.Hit");

        int fails = 0;
        void CheckZero(string prop) {
            try {
                var v = (int)hit.GetProperty(prop, BindingFlags.Public | BindingFlags.Static).GetValue(null);
                bool ok = v == 0; // scrController.instance is null in this harness -> must default to 0, never throw
                Console.WriteLine((ok ? "PASS " : "FAIL ") + $"{buildLabel}: {prop} == 0 (got {v})");
                if (!ok) fails++;
            } catch (Exception e) {
                Console.WriteLine($"FAIL {buildLabel}: {prop} threw {e.InnerException?.GetType().Name ?? e.GetType().Name}");
                fails++;
            }
        }
        foreach (var p in new[] { "OTE", "OVE", "OEP", "OLP", "OVL", "OTL", "OA", "OPM", "OXP", "OPL", "MissCount", "Overloads" }) {
            CheckZero(p);
        }
        Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILED");
        return fails;
    }
}
```

- [ ] **Step 2: Run it to verify the current code already limps along on alpha but should be re-checked once Task 3 lands on 148/145**

```bash
mcs /tmp/mvtest/Task3.cs -out:/tmp/mvtest/Task3.exe
mono /tmp/mvtest/Task3.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer/Overlayer/bin/Release/Overlayer.dll 148
```
Expected at this point (before Step 3's fix): all PASS, because with `scrController.instance == null` the `?.` chain already short-circuits to `?? 0` regardless of the `(int)HitMargin.X` cast inside it — this harness alone can't observe the wrong-slot bug without a live controller. That's expected and fine; the real regression check for this task is Step 6 (no exception with a genuinely out-of-range literal), which is the part that *does* fail before the fix. Proceed to Step 3 regardless.

- [ ] **Step 3: Replace the raw `(int)HitMargin.X` casts with `GameMargin`**

In `Overlayer/Tags/Hit.cs`, replace lines 222-256:
```csharp
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
```
with:
```csharp
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
```

(`nameof(HitMargin.PerfectMinus)` etc. compile fine even though the *runtime* type on 148/145 lacks that member — `nameof` only needs the member to exist in the assembly Overlayer is *compiled against*, which is always alpha.)

- [ ] **Step 4: Replace `MissCount`/`Overloads`**

In `Overlayer/Tags/Hit.cs`, replace lines 276-281:
```csharp
    [Tag]
    [TagDesc("Number of Misses")]
    public static int MissCount => scrController.instance?.playerOne?.marginTracker?.GetHits(HitMargin.FailMiss) ?? 0;
    [Tag]
    [TagDesc("Number of Overloads")]
    public static int Overloads => scrController.instance?.playerOne?.marginTracker?.GetHits(HitMargin.FailOverload) ?? 0;
```
with:
```csharp
    [Tag]
    [TagDesc("Number of Misses")]
    public static int MissCount => GameMargin.TryGet(nameof(HitMargin.FailMiss)) is HitMargin m ? scrController.instance?.playerOne?.marginTracker?.GetHits(m) ?? 0 : 0;
    [Tag]
    [TagDesc("Number of Overloads")]
    public static int Overloads => GameMargin.TryGet(nameof(HitMargin.FailOverload)) is HitMargin m ? scrController.instance?.playerOne?.marginTracker?.GetHits(m) ?? 0 : 0;
```

- [ ] **Step 5: Build and rerun the Step 1 harness on all three builds**

```bash
dotnet build Overlayer/Overlayer.csproj -c Release --nologo
for spec in "150:/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" "148:$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" "145:$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed"; do
  v=${spec%%:*}; m=${spec#*:}
  mono /tmp/mvtest/Task3.exe "$m" Overlayer/bin/Release/Overlayer.dll "$v"
done
```
Expected: `ALL PASS` for all three (still no exceptions, still all zero with no controller — the *fix's* observable effect, a correct nonzero slot with a real controller, needs a live game and is covered by the manual in-game check at the end of the plan).

- [ ] **Step 6: Prove the pre-fix code really would have crashed (Review Focus regression check)**

This step documents *why* the fix matters, using the already-built pre-fix DLL is no longer available (Task 3's Step 3 overwrote it) — instead, directly demonstrate the underlying hazard against the 148 assembly with a tiny standalone script that mimics the *old* code's cast:
```csharp
// /tmp/mvtest/Task3Regression.cs
using System;
using System.IO;
using System.Reflection;

class T {
    static int Main(string[] args) {
        var managed = args[0]; // point at ~/adofai-versions/general/.../Managed
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        var asm = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var hitMarginType = asm.GetType("HitMargin");
        var array = new int[Enum.GetValues(hitMarginType).Length]; // same sizing scrMarginTracker uses
        try {
            // Overlayer's OLD code did: hitMarginsCount[(int)HitMargin.PerfectMinus] where
            // PerfectMinus is alpha's compiled literal 3 - safe on 148 since 3 < array length.
            // But e.g. HitMargin.Midspin (alpha literal 14) is what P_scrMistakeManager.cs's old
            // GetHits(..., HitMargin.Midspin, ...) call passed - out of range on 148's shorter enum.
            const int midspinAlphaLiteral = 14;
            var _ = array[midspinAlphaLiteral];
            Console.WriteLine("FAIL: expected IndexOutOfRangeException, none thrown (array length " + array.Length + ")");
            return 1;
        } catch (IndexOutOfRangeException) {
            Console.WriteLine("PASS: confirmed the old raw-literal approach throws on 148's shorter HitMargin enum (array length " + array.Length + ")");
            return 0;
        }
    }
}
```
```bash
mcs /tmp/mvtest/Task3Regression.cs -out:/tmp/mvtest/Task3Regression.exe
mono /tmp/mvtest/Task3Regression.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed"
```
Expected: `PASS: confirmed ...`. This is the exact crash Task 6 (below) also avoids for the same reason via `GameMargin.Existing`.

- [ ] **Step 7: Commit**

```bash
git add Overlayer/Tags/Hit.cs
git commit -m "Hit.cs: resolve game-reported margin counts by name, not by literal

OTE..OPL indexed the game's own hitMarginsCount array with Overlayer's
alpha-compiled HitMargin literal, which points at the wrong slot (or out
of bounds) on 148/145 where the enum has fewer members at different
values. MissCount/Overloads had the same issue calling GetHits(HitMargin.
FailMiss/FailOverload). Route both through GameMargin.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 4: `Hit.cs` — display text via name-safe lookup (`LHit`/`NHit`/`SHit`/`CHit`)

**Files:**
- Modify: `Overlayer/Tags/Hit.cs:21-32`

**Interfaces:**
- Consumes: nothing from earlier tasks (pure `Hit.cs`-internal change).
- Produces: no change to `LHit`/`NHit`/`SHit`/`CHit`'s signatures.

- [ ] **Step 1: Write the failing verification harness**

Create `/tmp/mvtest/Task4.cs`. This calls the *new* private `NameOf`/`HitText` methods directly via reflection (they don't exist yet, so this fails until Step 2 lands), covering all 10 values `Lenient`/`Normal`/`Strict`/`Current` can ever hold (the 9 angle-classification values plus `FailMiss`, which `Hit.FixMargin`'s `noFailInfiniteMargin` branch can also produce):
```csharp
using System;
using System.IO;
using System.Reflection;

static class Task4 {
    static int Main(string[] args) {
        var managed = args[0];
        var overlayerDll = args[1];
        var buildLabel = args[2];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        var game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var ov = Assembly.LoadFrom(overlayerDll);
        var hit = ov.GetType("Overlayer.Tags.Hit");
        var hitMarginType = game.GetType("HitMargin");
        var nameOf = hit.GetMethod("NameOf", BindingFlags.NonPublic | BindingFlags.Static);
        var hitText = hit.GetMethod("HitText", BindingFlags.NonPublic | BindingFlags.Static);
        if (nameOf == null || hitText == null) { Console.WriteLine("FAIL: NameOf/HitText not found"); return 1; }

        int fails = 0;
        void Check(string memberName, string expectedName) {
            // Look the member up on OVERLAYER's compiled HitMargin (alpha), matching what
            // Hit.Lenient/etc. actually hold at runtime (Overlayer's own literal, not the
            // loaded type's numbering) - see the spec's "self-consistent" section.
            var alphaGame = Assembly.LoadFrom("/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed/Assembly-CSharp.dll");
            var alphaHitMargin = alphaGame.GetType("HitMargin");
            var value = Enum.Parse(alphaHitMargin, memberName);
            var name = (string)nameOf.Invoke(null, new object[] { value });
            bool ok = name == expectedName;
            Console.WriteLine((ok ? "PASS " : "FAIL ") + $"{buildLabel}: NameOf({memberName}) == \"{expectedName}\" (got \"{name}\")");
            if (!ok) fails++;
        }
        foreach (var n in new[] { "TooEarly", "VeryEarly", "EarlyPerfect", "PerfectMinus", "XPerfect", "PerfectPlus", "LatePerfect", "VeryLate", "TooLate", "FailMiss" }) {
            Check(n, n);
        }
        Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILED");
        return fails;
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
mcs /tmp/mvtest/Task4.cs -out:/tmp/mvtest/Task4.exe
mono /tmp/mvtest/Task4.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer/Overlayer/bin/Release/Overlayer.dll 148
```
Expected: `FAIL: NameOf/HitText not found`.

- [ ] **Step 3: Replace `LHit`/`NHit`/`SHit`/`CHit`**

In `Overlayer/Tags/Hit.cs`, replace lines 21-32:
```csharp
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
```
with:
```csharp
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
```

- [ ] **Step 4: Build and rerun the harness on all three builds**

```bash
dotnet build Overlayer/Overlayer.csproj -c Release --nologo
for spec in "150:/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" "148:$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" "145:$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed"; do
  v=${spec%%:*}; m=${spec#*:}
  mono /tmp/mvtest/Task4.exe "$m" Overlayer/bin/Release/Overlayer.dll "$v"
done
```
Expected: `ALL PASS` for all three — `NameOf` never depends on which build is loaded (it's Overlayer's own literal switch), so it produces the same fixed English name everywhere.

- [ ] **Step 5: Verify the `HitText` fallback against the real 148 localization table**

Create `/tmp/mvtest/Task4Fallback.cs`:
```csharp
using System;
using System.IO;
using System.Reflection;

static class Task4Fallback {
    static int Main(string[] args) {
        var managed = args[0];
        var overlayerDll = args[1];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        var game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var ov = Assembly.LoadFrom(overlayerDll);
        var hit = ov.GetType("Overlayer.Tags.Hit");
        var hitText = hit.GetMethod("HitText", BindingFlags.NonPublic | BindingFlags.Static);
        var alphaGame = Assembly.LoadFrom("/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed/Assembly-CSharp.dll");
        var xperfect = Enum.Parse(alphaGame.GetType("HitMargin"), "XPerfect");
        try {
            var text = (string)hitText.Invoke(null, new object[] { xperfect });
            Console.WriteLine("HitText(XPerfect) on this build = \"" + text + "\"");
            // Can't assert a specific non-empty localized string without RDString's Setup()
            // having run in a real game process (it needs a loaded localization CSV), so this
            // step is a manual smoke check, not an automated PASS/FAIL - it must at least not throw.
            Console.WriteLine("PASS: no exception");
            return 0;
        } catch (Exception e) {
            Console.WriteLine("FAIL: threw " + (e.InnerException ?? e));
            return 1;
        }
    }
}
```
```bash
mcs /tmp/mvtest/Task4Fallback.cs -out:/tmp/mvtest/Task4Fallback.exe
mono /tmp/mvtest/Task4Fallback.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll
```
Expected: `PASS: no exception` (confirming the code path runs without crashing outside a live game; the actual localized text can only be confirmed in-game, per the plan's manual check at the end).

- [ ] **Step 6: Commit**

```bash
git add Overlayer/Tags/Hit.cs
git commit -m "Hit.cs: fix LHit/NHit/SHit/CHit to not depend on the loaded HitMargin's names

\"HitMargin.\" + margin (implicit ToString()) looks the value up in the
RUNTIME-loaded HitMargin type's own name table, not Overlayer's compiled
intent - wrong text on any build whose enum differs from alpha. NameOf
maps via a switch over Overlayer's own compiled literals instead (safe -
both sides are Overlayer's literals). Pre-150 also has no PerfectMinus/
XPerfect/PerfectPlus localization entries, only \"Perfect\" - fall back to
that key for exactly those three values.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 5: `P_scrMisc.cs` — version-gated old/new `GetHitMargin` patches

**Files:**
- Modify: `Overlayer/Tags/Patches/P_scrMisc.cs`

**Interfaces:**
- Consumes: `GameVersion.Current` is not referenced directly here — version gating goes through `LazyPatchAttribute.MinVersion`/`MaxVersion`, which already reads `GameVersion.Current` via `CurrentVersion` (Task 1).
- Produces: no change to any tag; only which Harmony patch class targets the game method changes.

- [ ] **Step 1: Write the failing verification harness (reuse the validator)**

This task's correctness is entirely "does `LazyPatchValidator` report 0 issues on every build," so it reuses the harness from [[lazypatch-validation]] in memory rather than a new bespoke script. If it doesn't exist in this session, recreate it at `/tmp/mvtest/Harness.cs` following that memory note's description (load the game assembly, load Overlayer.dll, reflect over every type's `LazyPatchAttribute` instances, call `LazyPatchValidator.Validate` on each, print issues).

Run it against 148 and 145 *before* this task's change, to see the current (known, expected-for-now) failures:
```bash
mono /tmp/mvtest/Harness.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer/Overlayer/bin/Release/Overlayer.dll neg
```
Expected: `46 patches checked, 2 with issues` — the two `GetHitMarginInDeg`/`GetHitMarginInSec` targets not found on 148 (the old signature is still `GetHitMargin`).

- [ ] **Step 2: Add `MinVersion = 150` to the six existing classes**

In `Overlayer/Tags/Patches/P_scrMisc.cs`, add `MinVersion = 150,` as the first named property in each of the six existing `[LazyPatch(...)]` attributes (`Hit__GetHitMarginInDeg`, `Hit__GetHitMarginInSec`, `Combo__GetHitMarginInDeg`, `Combo__GetHitMarginInSec`, `Scores__GetHitMarginInDeg`, `Scores__GetHitMarginInSec`). For example, the first one changes from:
```csharp
    [LazyPatch("Tags.P_scrMisc.Hit__GetHitMarginInDeg", "scrMisc", "GetHitMarginInDeg", Triggers =
    [
```
to:
```csharp
    [LazyPatch("Tags.P_scrMisc.Hit__GetHitMarginInDeg", "scrMisc", "GetHitMarginInDeg", MinVersion = 150, Triggers =
    [
```
Apply the same one-line change to the other five `[LazyPatch(...)]` attributes in this file (they all follow the identical `"Tags.P_scrMisc.X__GetHitMarginInSec", "scrMisc", "GetHitMarginInSec"` or `InDeg` pattern — add `MinVersion = 150,` right after the third string argument in each).

- [ ] **Step 3: Add three new classes for the old `GetHitMargin` signature**

The old game method (145 and 148, confirmed identical signature in both decompiles) is:
```csharp
public static HitMargin GetHitMargin(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale = 1.0)
```
Add these three classes to `Overlayer/Tags/Patches/P_scrMisc.cs`, inside the existing `#region Hit` / `#region ComboStats` / `#region Scores` blocks respectively (right after each region's existing pair of classes, before the matching `#endregion`):

In `#region Hit`, after `Hit__GetHitMarginInSec`:
```csharp
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
        nameof(Hit.LSlow), nameof(Hit.NSlow), nameof(Hit.SSlow), nameof(Hit.CSlow)
    ])]
    public static class Hit__GetHitMargin {
        public static bool Prefix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return true;
            }
            ApplyHit(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                ref __result);
            return false;
        }
    }
```

In `#region ComboStats`, after `Combo__GetHitMarginInSec`:
```csharp
    [LazyPatch("Tags.P_scrMisc.Combo__GetHitMargin", "scrMisc", "GetHitMargin", MaxVersion = 149, Triggers =
    [
        nameof(ComboStats.Combo), nameof(ComboStats.MaxCombo),
        nameof(ComboStats.LMarginCombo), nameof(ComboStats.NMarginCombo), nameof(ComboStats.SMarginCombo), nameof(ComboStats.MarginCombo),
        nameof(ComboStats.LMarginMaxCombo), nameof(ComboStats.NMarginMaxCombo), nameof(ComboStats.SMarginMaxCombo), nameof(ComboStats.MarginMaxCombo),
        nameof(ComboStats.LMarginCombos), nameof(ComboStats.NMarginCombos), nameof(ComboStats.SMarginCombos), nameof(ComboStats.MarginCombos),
        nameof(ComboStats.LMarginMaxCombos), nameof(ComboStats.NMarginMaxCombos), nameof(ComboStats.SMarginMaxCombos), nameof(ComboStats.MarginMaxCombos),
        nameof(ComboStats.SpecialPlayMark)
    ])]
    public static class Combo__GetHitMargin {
        public static void Postfix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyCombo(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                __result);
        }
    }
```

In `#region Scores`, after `Scores__GetHitMarginInSec`:
```csharp
    [LazyPatch("Tags.P_scrMisc.Scores__GetHitMargin", "scrMisc", "GetHitMargin", MaxVersion = 149, Triggers =
    [
        nameof(Scores.LScore), nameof(Scores.NScore), nameof(Scores.SScore), nameof(Scores.Score)
    ])]
    public static class Scores__GetHitMargin {
        public static void Postfix(float hitangle, float refangle, bool isCW, float bpmTimesSpeed, float conductorPitch, double marginScale, ref HitMargin __result) {
            var controller = scrController.instance;
            if(IsFreeroam(controller)) {
                return;
            }
            ApplyScores(controller,
                Hit.GetHitMarginInDeg(Difficulty.Lenient, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Normal, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                Hit.GetHitMarginInDeg(Difficulty.Strict, hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch, marginScale),
                __result);
        }
    }
```

- [ ] **Step 4: Build and rerun the validator harness on all three builds**

```bash
dotnet build Overlayer/Overlayer.csproj -c Release --nologo
mono /tmp/mvtest/Harness.exe "/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" Overlayer/bin/Release/Overlayer.dll neg
mono /tmp/mvtest/Harness.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll
mono /tmp/mvtest/Harness.exe "$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll
```
Expected: `49 patches checked, 0 with issues` on alpha (46 + 3 new ones, all `IsCompatible == false` there so skipped, not flagged), and `49 patches checked, 0 with issues` on 148 and 145 too (the 6 alpha-only classes are `IsCompatible == false` there instead).

- [ ] **Step 5: Verify `MinVersion`/`MaxVersion` degrade gracefully for a release number no in-scope build actually has**

None of 145/148/150 sits exactly at the `MinVersion`/`MaxVersion` boundary this task introduces (149) or far outside it (0, 1000) — verify `LazyPatchAttribute.IsCompatible`'s arithmetic directly instead, without needing a matching real assembly:
```csharp
// /tmp/mvtest/Task5Boundary.cs
using System;
using System.IO;
using System.Reflection;

static class Task5Boundary {
    static int Main(string[] args) {
        var overlayerDll = args[0];
        var ov = Assembly.LoadFrom(overlayerDll);
        var attrType = ov.GetType("Overlayer.Core.Patches.LazyPatchAttribute");
        var isCompatible = attrType.GetProperty("IsCompatible");

        int fails = 0;
        void Check(int min, int max, string label, bool expected) {
            var attr = Activator.CreateInstance(attrType, "t", "SomeType", "SomeMethod");
            attrType.GetProperty("MinVersion").SetValue(attr, min);
            attrType.GetProperty("MaxVersion").SetValue(attr, max);
            // CurrentVersion is a static readonly field baked at type-load time from the loaded
            // game assembly (149 isn't a real build), so this checks the MinVersion/MaxVersion
            // comparison arithmetic directly against whatever CurrentVersion this process loaded
            // with - it's the boundary LOGIC being verified, not a specific game build's outcome.
            var current = (int)attrType.GetField("CurrentVersion").GetValue(null);
            bool actual = (current >= min || min < 0) && (max >= current || max < 0);
            bool ok = actual == expected;
            Console.WriteLine((ok ? "PASS " : "FAIL ") + $"{label} (CurrentVersion={current}, min={min}, max={max}) -> {actual}");
            if (!ok) fails++;
        }
        // A gap version (149, between the old and new patch sets) must not satisfy BOTH a
        // MinVersion=150 patch and a MaxVersion=149 patch's compatibility check at once - each
        // gate is independently correct regardless of what CurrentVersion actually loaded as.
        Check(150, -1, "MinVersion=150 gate", args.Length > 1 && int.Parse(args[1]) >= 150);
        Check(-1, 149, "MaxVersion=149 gate", args.Length > 1 && int.Parse(args[1]) <= 149);
        Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILED");
        return fails;
    }
}
```
```bash
mcs /tmp/mvtest/Task5Boundary.cs -out:/tmp/mvtest/Task5Boundary.exe
mono /tmp/mvtest/Task5Boundary.exe /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer/Overlayer/bin/Release/Overlayer.dll 150
```
Expected: `ALL PASS` (with `CurrentVersion=150`, the `MinVersion=150` gate is satisfied and the `MaxVersion=149` gate is not — i.e. exactly one of the two patch sets is ever active for any given `CurrentVersion`, never both and never neither, confirming the gap at 149 is handled by simple `>=`/`<=` comparison, not a lookup table that could miss an unlisted value).

- [ ] **Step 6: Commit**

```bash
git add Overlayer/Tags/Patches/P_scrMisc.cs
git commit -m "P_scrMisc.cs: patch the old scrMisc.GetHitMargin on pre-150 builds

The r150 GetHitMargin -> GetHitMarginInDeg/InSec split only exists on
150+; 148/145 still have the single old-signature GetHitMargin. Gate the
existing InDeg/InSec patches to MinVersion=150 and add mirrored classes
for the old signature at MaxVersion=149, sharing the same Apply* helpers
and Hit.GetHitMarginInDeg classification (version-independent angle math).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 6: `P_scrMistakeManager.cs` — `GameMargin`-safe `GetHits` calls + version-gated X-Score fields

**Files:**
- Modify: `Overlayer/Tags/Patches/P_scrMistakeManager.cs`

**Interfaces:**
- Consumes: `GameMargin.Existing`/`GameMargin.TryGet` (Task 2), `GameVersion.Current` (Task 1).
- Produces: no change to `AccuracyStats.Accuracy`/`XAccuracy`/`AbsXAccuracy`/`MaxAccuracy` on any build. `AccuracyStats.MaxXAccuracy`/`AbsMaxXAccuracy` and `Scores.XScore`/`MaxXScore` become `0`/`double.NaN` on 148/145 instead of crashing.

- [ ] **Step 1: Write the failing verification harness**

This exercises exactly the crash Task 3's Step 6 demonstrated in isolation, but against the real, full `AccuracyStats__CalculatePercentAcc.Postfix` method (reflection-invoked directly, without a live game, to prove the *method itself* JIT-resolves cleanly on 148/145 - a live-game-only aspect, like the specific numeric output, is covered by the manual in-game check at the end of the plan):
```csharp
using System;
using System.IO;
using System.Reflection;

static class Task6 {
    static int Main(string[] args) {
        var managed = args[0];
        var overlayerDll = args[1];
        var buildLabel = args[2];
        var expectXScoreFieldsExist = bool.Parse(args[3]); // true only for the 150 assembly
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var ov = Assembly.LoadFrom(overlayerDll);
        var patchType = ov.GetType("Overlayer.Tags.Patches.P_scrMistakeManager")
            .GetNestedType("AccuracyStats__CalculatePercentAcc", BindingFlags.Public | BindingFlags.NonPublic);
        var postfix = patchType.GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        // Calling with a null __instance mirrors "not in a real hit right now" - the method's
        // own null checks (ADOBase.controller etc.) must handle this, same as any other tag.
        try {
            postfix.Invoke(null, new object[] { null });
            Console.WriteLine($"PASS {buildLabel}: Postfix did not throw with __instance == null");
            return 0;
        } catch (Exception e) {
            Console.WriteLine($"FAIL {buildLabel}: threw {(e.InnerException ?? e)}");
            return 1;
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails on 148/145 (current code references nonexistent members)**

```bash
mcs /tmp/mvtest/Task6.cs -out:/tmp/mvtest/Task6.exe
mono /tmp/mvtest/Task6.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer/Overlayer/bin/Release/Overlayer.dll 148 false
```
Expected: `FAIL 148: threw System.NullReferenceException` (from `ADOBase.controller.playerOne...` with no live controller) **or** a `MissingMemberException`/`TypeLoadException` mentioning `xScore`/`maxXScore`/`maxPossibleXAcc` — either way, this specific harness call pattern can't fully isolate the JIT-resolution failure from the expected null-controller failure yet. Proceed to Step 3 and rely on Step 5's more targeted check instead, which is the one that actually pins this task's core risk.

- [ ] **Step 3: Split the alpha-only fields into their own method, gated by `GameVersion.Current`**

This task depends on the three tag fields Task 7 adds (`AccuracyStats.CAccuracy`/`CXAccuracy`, `Scores.CXScore`) — **do Task 7's Step 1 (field declarations only) first**, then come back here. Task 7's own test steps run after this task, since they need `ApplyVersionIndependentStats` (defined below) to exist.

In `Overlayer/Tags/Patches/P_scrMistakeManager.cs`, replace the whole file with:
```csharp
using Overlayer.Core.Patches;
using Overlayer.Tags;
using Overlayer.Utils;
using System;

namespace Overlayer.Tags.Patches;

public class P_scrMistakeManager : PatchBase<P_scrMistakeManager> {
    [LazyPatch("Tags.P_scrMistakeManager.AccuracyStats__CalculatePercentAcc", "scrMistakesManager", "CalculateTotalAccuracy", Triggers =
    [
        nameof(AccuracyStats.Accuracy), nameof(AccuracyStats.MaxAccuracy),
        nameof(AccuracyStats.XAccuracy), nameof(AccuracyStats.MaxXAccuracy),
        nameof(AccuracyStats.AbsXAccuracy), nameof(AccuracyStats.AbsMaxXAccuracy),
        nameof(Scores.XScore), nameof(Scores.MaxXScore),
        nameof(AccuracyStats.CAccuracy), nameof(AccuracyStats.CXAccuracy), nameof(Scores.CXScore)
    ])]
    public static class AccuracyStats__CalculatePercentAcc {
        // Only called from Postfix when GameVersion.Current >= 150. Mono resolves a method's IL
        // as a whole the first time it's CALLED, not per branch - xScore/maxXScore/maxPossibleXAcc
        // don't exist at all on scrMarginTracker pre-150 (confirmed directly in the 148 decompile,
        // not just a numbering difference), so a reference to them anywhere in a method that DOES
        // get called on 148/145 would fail even on a branch that's never taken. Keeping them in a
        // separate, conditionally-CALLED method is what actually protects against that.
        static void ApplyAlphaOnlyXScoreFields(scrMarginTracker tracker, double checkpointMinus) {
            AccuracyStats.AbsMaxXAccuracy = 100.0 * tracker.maxPossibleXAcc;
            AccuracyStats.MaxXAccuracy = AccuracyStats.AbsMaxXAccuracy * checkpointMinus;
            Scores.XScore = tracker.xScore;
            Scores.MaxXScore = tracker.maxXScore;
        }

        static int GameCount(scrMarginTracker tracker, string name) => GameMargin.TryGet(name) is HitMargin m ? tracker.GetHits(m) : 0;

        // Split out from Postfix so a test harness can exercise it directly without a live
        // scrController/ADOBase.controller - everything it reads is either Hit's own static
        // counters (self-consistent on every build) or GameMargin-resolved state via the
        // tracker passed in (also safe on every build, unlike a raw HitMargin literal).
        public static void ApplyVersionIndependentStats(scrMarginTracker tracker) {
            int autoCount = GameCount(tracker, nameof(HitMargin.Auto));
            int midspinCount = GameCount(tracker, nameof(HitMargin.Midspin)); // 0 pre-150: that build's Auto slot already merged this in, see spec §2
            int multipressCount = GameCount(tracker, nameof(HitMargin.Multipress));
            int failMissCount = GameCount(tracker, nameof(HitMargin.FailMiss));
            int failOverloadCount = GameCount(tracker, nameof(HitMargin.FailOverload));
            int failedFloorCount = GameCount(tracker, nameof(HitMargin.FailedFloor));
            int deaths = tracker.GetDeaths();

            // Mirrors HitMarginHelper.PlayerHitMarginWeights: XPerfect/PerfectMinus/PerfectPlus
            // and the forced-perfect override states (Auto/Midspin/Multipress) = full success;
            // EarlyPerfect/LatePerfect = full success too for CAccuracy (mirrors
            // HitMarginHelper.PerfectHitMargins/SemiPerfectHitMargins, alpha's plain-Accuracy
            // formula - see spec §5's table), but only 0.75-weighted for CXAccuracy.
            int perfectFamily = Hit.CP + autoCount + midspinCount + multipressCount;
            int semiPerfect = Hit.CEP + Hit.CLP;

            int accDenominator = Hit.CTE + Hit.CVE + Hit.CEP + Hit.CP + Hit.CLP + Hit.CVL + Hit.CTL
                + autoCount + midspinCount + multipressCount + failMissCount + failOverloadCount + failedFloorCount + deaths;
            int accNumerator = perfectFamily + semiPerfect;
            double accRatio = accDenominator == 0 ? 0.0 : (accNumerator == accDenominator ? 1.0 : (double)accNumerator / accDenominator);
            double accBonus = (perfectFamily * 0.0001) - (failedFloorCount * 0.0001);
            AccuracyStats.CAccuracy = 100.0 * (accRatio + accBonus);

            double xWeighted = (Hit.CP * 1.0) + ((Hit.CEP + Hit.CLP) * 0.75) + ((Hit.CVE + Hit.CVL) * 0.4) + ((Hit.CTE + Hit.CTL) * 0.2) + (failedFloorCount * 0.2);
            int xDenominator = Hit.CTE + Hit.CVE + Hit.CEP + Hit.CP + Hit.CLP + Hit.CVL + Hit.CTL + failedFloorCount + failMissCount + failOverloadCount;
            AccuracyStats.CXAccuracy = xDenominator == 0 ? 0.0 : 100.0 * (xWeighted / xDenominator);

            // Mirrors HitMarginHelper.HitMarginXScores exactly: XPerfect=2, PerfectMinus/PerfectPlus=1, everything else=0.
            Scores.CXScore = (Hit.CXP * 2) + Hit.CPM + Hit.CPP;
        }

        public static void Postfix(scrMistakesManager __instance) {
            // scrMarginTracker.CalculatePercentAcc() (called on every hit, before this
            // postfix runs) already computes percentAcc/percentXAcc using the game's own
            // up-to-date weights, so read those directly instead of re-deriving them here.
            // percentAcc/percentXAcc exist and are populated on every in-scope build (145/148/150);
            // maxPossibleXAcc/xScore/maxXScore are alpha-only - see ApplyAlphaOnlyXScoreFields.
            var tracker = ADOBase.controller?.playerOne?.marginTracker;
            if(tracker == null) {
                return;
            }

            double checkpointMinus = Math.Pow(0.9875, scrController.checkpointsUsed);

            AccuracyStats.Accuracy = 100.0 * tracker.percentAcc;
            AccuracyStats.XAccuracy = 100.0 * tracker.percentXAcc;
            AccuracyStats.AbsXAccuracy = AccuracyStats.XAccuracy / checkpointMinus;

            if(GameVersion.Current >= 150) {
                ApplyAlphaOnlyXScoreFields(tracker, checkpointMinus);
            } else {
                AccuracyStats.AbsMaxXAccuracy = double.NaN;
                AccuracyStats.MaxXAccuracy = double.NaN;
                Scores.XScore = 0;
                Scores.MaxXScore = 0;
            }

            if(ADOBase.lm is not null && ADOBase.lm.listFloors != null &&
                Tile.CurTile >= 0 && Tile.CurTile < ADOBase.lm.listFloors.Count &&
                ADOBase.lm.listFloors[Tile.CurTile] != null) {

                // Mirrors scrMarginTracker.CalculatePercentAcc()'s HitMarginHelper.PerfectHitMargins /
                // SemiPerfectHitMargins split, extended with the tiles left in the level as guaranteed
                // future perfects, to estimate the best Accuracy still reachable from here.
                // GameMargin.Existing resolves each name against the ACTUAL loaded HitMargin
                // (correct numbering on every build) and drops PerfectMinus/XPerfect/PerfectPlus/
                // Midspin/FailedFloor entirely on builds where they don't exist as named members -
                // the raw HitMargin literal this used before could read the wrong slot (e.g.
                // LatePerfect's alpha value lands on TooLate's slot on 148) or throw
                // IndexOutOfRangeException outright (Midspin/FailedFloor's alpha values are past
                // the end of 148's shorter enum).
                int perfectAll = tracker.GetHits(GameMargin.Existing(nameof(HitMargin.PerfectMinus), nameof(HitMargin.XPerfect), nameof(HitMargin.PerfectPlus), nameof(HitMargin.Auto), nameof(HitMargin.Midspin), nameof(HitMargin.Multipress)));
                int semiPerfect = tracker.GetHits(GameMargin.Existing(nameof(HitMargin.EarlyPerfect), nameof(HitMargin.LatePerfect)));
                int failedFloor = tracker.GetHits(GameMargin.Existing(nameof(HitMargin.FailedFloor)));
                int deaths = tracker.GetDeaths();

                int lefttile = Tile.LeftTile - (ADOBase.lm.listFloors[Tile.CurTile].midSpin ? 1 : 0);

                int mxsucess = lefttile + perfectAll + semiPerfect;
                int mxtotal = tracker.hitMargins.Count + lefttile + deaths;
                double mxratio = (mxsucess == mxtotal) ? 1.0 : ((double)mxsucess / mxtotal);
                double mxbonus = ((lefttile + perfectAll) * 0.0001) - (failedFloor * 0.0001);

                AccuracyStats.MaxAccuracy = 100.0 * (mxratio + mxbonus);
            }

            ApplyVersionIndependentStats(tracker);
        }
    }
}
```

(Note: the `Postfix`'s null-guard changed from the original's implicit `ADOBase.controller.playerOne.marginTracker` — which throws `NullReferenceException` if any link is null — to an explicit `tracker == null` early return, matching how every other tag in this codebase degrades outside a real hit. This is a small, deliberate robustness improvement needed for Step 1's harness to actually exercise the method without a live game; it does not change in-game behavior, since `ADOBase.controller`/`playerOne`/`marginTracker` are always non-null during a real hit.)

- [ ] **Step 4: Build**

```bash
dotnet build Overlayer/Overlayer.csproj -c Release --nologo
```
Expected: `오류 0개` (Task 7's Step 1 field declarations must already be in place, per the note above Step 3, or this fails to compile).

- [ ] **Step 5: Rerun Task 3's harness pattern against the real `Postfix`**

A build success in Step 4 does not, by itself, prove the JIT-safety fix works — C# always compiles fine against alpha (Overlayer's compile-time reference), regardless of what's loaded at runtime; the failure this task fixes only manifests when a *different* assembly is loaded. Run the harness from Task 6's Step 1 against all three builds:
```bash
mono /tmp/mvtest/Task6.exe "/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" Overlayer/bin/Release/Overlayer.dll 150 true
mono /tmp/mvtest/Task6.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll 148 false
mono /tmp/mvtest/Task6.exe "$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll 145 false
```
Expected: `PASS ...: Postfix did not throw with __instance == null` for all three (the `tracker == null` guard added in Step 3 makes this return immediately before touching any version-specific member, so this specifically proves the method's IL resolves cleanly on 148/145 — the exact risk this task exists to fix).

- [ ] **Step 6: Verify the `MaxAccuracy` `GetHits(GameMargin.Existing(...))` calls don't throw on 148/145**

Step 5's harness returns before reaching these calls (the `tracker == null` guard short-circuits `Postfix` first), so this is the step that actually exercises the exact array-bounds/wrong-slot risk `GameMargin.Existing` fixes here. Create `/tmp/mvtest/Task6GetHits.cs`:
```csharp
using System;
using System.IO;
using System.Reflection;

static class Task6GetHits {
    static int Main(string[] args) {
        var managed = args[0];
        var overlayerDll = args[1];
        var buildLabel = args[2];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        var game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var ov = Assembly.LoadFrom(overlayerDll);
        var gameMargin = ov.GetType("Overlayer.Tags.GameMargin");
        var existing = gameMargin.GetMethod("Existing", BindingFlags.Public | BindingFlags.Static);
        var tracker = Activator.CreateInstance(game.GetType("scrMarginTracker"));
        var getHits = tracker.GetType().GetMethod("GetHits", new[] { existing.ReturnType });

        try {
            // The exact three GameMargin.Existing(...) calls from MaxAccuracy's computation -
            // on 148/145 this drops PerfectMinus/XPerfect/PerfectPlus/Midspin (perfectAll) and
            // FailedFloor (failedFloor) entirely instead of using their out-of-range alpha values.
            var perfectAll = existing.Invoke(null, new object[] { new[] { "PerfectMinus", "XPerfect", "PerfectPlus", "Auto", "Midspin", "Multipress" } });
            var semiPerfect = existing.Invoke(null, new object[] { new[] { "EarlyPerfect", "LatePerfect" } });
            var failedFloor = existing.Invoke(null, new object[] { new[] { "FailedFloor" } });
            getHits.Invoke(tracker, new[] { perfectAll });
            getHits.Invoke(tracker, new[] { semiPerfect });
            getHits.Invoke(tracker, new[] { failedFloor });
            Console.WriteLine($"PASS {buildLabel}: all three GetHits(GameMargin.Existing(...)) calls ran without throwing");
            return 0;
        } catch (Exception e) {
            Console.WriteLine($"FAIL {buildLabel}: threw {(e.InnerException ?? e)}");
            return 1;
        }
    }
}
```
```bash
mcs /tmp/mvtest/Task6GetHits.cs -out:/tmp/mvtest/Task6GetHits.exe
mono /tmp/mvtest/Task6GetHits.exe "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll 148
mono /tmp/mvtest/Task6GetHits.exe "$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed" Overlayer/bin/Release/Overlayer.dll 145
```
Expected: `PASS` for both — confirm this same harness against the *old* raw-literal approach would have thrown by rerunning Task 3's Step 6 script, which demonstrates exactly that.

- [ ] **Step 7: Commit**

```bash
git add Overlayer/Tags/Patches/P_scrMistakeManager.cs
git commit -m "P_scrMistakeManager.cs: GameMargin-safe GetHits, gate alpha-only X-Score fields

xScore/maxXScore/maxPossibleXAcc don't exist at all on scrMarginTracker
pre-150 (not a numbering difference - confirmed absent in the 148/145
decompile). Move their reads into a method only called when
GameVersion.Current >= 150, since Mono resolves a method's IL as a whole
the first time it's called, not per branch.

The three GetHits(HitMargin.X, ...) calls in MaxAccuracy's calculation
used Overlayer's alpha-compiled literals directly, which either read the
wrong slot (LatePerfect) or throw IndexOutOfRangeException outright
(Midspin/FailedFloor, past the end of 148/145's shorter enum) - route
through GameMargin.Existing.

Also adds CAccuracy/CXAccuracy/CXScore: version-independent, always
alpha's exclusion philosophy for Auto/Midspin, computed from Overlayer's
own self-consistent counters (see Tags/AccuracyStats.cs, Tags/Scores.cs).

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 7: `CAccuracy`/`CXAccuracy`/`CXScore` tag fields

**Files:**
- Modify: `Overlayer/Tags/AccuracyStats.cs`
- Modify: `Overlayer/Tags/Scores.cs`

**Interfaces:**
- Produces: `AccuracyStats.CAccuracy` (`double`), `AccuracyStats.CXAccuracy` (`double`), `Scores.CXScore` (`int`) — the three fields Task 6's `Postfix` writes to.

**Note:** per Task 6's Step 4, execute this task's field declarations *before* Task 6, since Task 6's code references these fields. Its *tests*, below, run after Task 6 lands (they call `ApplyVersionIndependentStats`, which Task 6 defines).

- [ ] **Step 1: Add the fields**

In `Overlayer/Tags/AccuracyStats.cs`, replace:
```csharp
public static class AccuracyStats {
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Accuracy(when perfect: 100+0.01%)")]
    public static double Accuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("XAccuracy(when pure perfect: 100)")]
    public static double XAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Absolute Max XAccuracy(Excluding checkpoints)")]
    public static double AbsXAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable Accuracy in the current tile")]
    public static double MaxAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable XAccuracy in the current tile")]
    public static double MaxXAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable Absolute Max XAccuracy in the current tile(Excluding checkpoints)")]
    public static double AbsMaxXAccuracy;

    public static void Reset() => Accuracy = XAccuracy = MaxAccuracy = AbsXAccuracy = MaxXAccuracy = AbsMaxXAccuracy = double.NaN;
}
```
with:
```csharp
public static class AccuracyStats {
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Accuracy(when perfect: 100+0.01%)")]
    public static double Accuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("XAccuracy(when pure perfect: 100)")]
    public static double XAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Absolute Max XAccuracy(Excluding checkpoints)")]
    public static double AbsXAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable Accuracy in the current tile")]
    public static double MaxAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable XAccuracy in the current tile")]
    public static double MaxXAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("The highest achievable Absolute Max XAccuracy in the current tile(Excluding checkpoints)")]
    public static double AbsMaxXAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("Accuracy computed the same way on every ADOFAI version, regardless of which one is running")]
    public static double CAccuracy;
    [Tag(ProcessingFlags = ValueProcessing.RoundNumber)]
    [TagDesc("XAccuracy computed the same way on every ADOFAI version (Auto/Midspin never count), regardless of which one is running")]
    public static double CXAccuracy;

    public static void Reset() => Accuracy = XAccuracy = MaxAccuracy = AbsXAccuracy = MaxXAccuracy = AbsMaxXAccuracy = CAccuracy = CXAccuracy = double.NaN;
}
```

In `Overlayer/Tags/Scores.cs`, replace:
```csharp
    [Tag]
    [TagDesc("X-Score(2 per XPerfect, 1 per -Perfect/Perfect+, 0 otherwise)")]
    public static int XScore;
    [Tag]
    [TagDesc("Maximum possible X-Score for this level((Total Tile - Autoplay - Midspin) x 2)")]
    public static int MaxXScore;
```
with:
```csharp
    [Tag]
    [TagDesc("X-Score(2 per XPerfect, 1 per -Perfect/Perfect+, 0 otherwise)")]
    public static int XScore;
    [Tag]
    [TagDesc("Maximum possible X-Score for this level((Total Tile - Autoplay - Midspin) x 2)")]
    public static int MaxXScore;
    [Tag]
    [TagDesc("X-Score computed the same way on every ADOFAI version, regardless of which one is running")]
    public static int CXScore;
```
and:
```csharp
    public static void Reset() => LScore = NScore = SScore = Score = XScore = MaxXScore = 0;
```
with:
```csharp
    public static void Reset() => LScore = NScore = SScore = Score = XScore = MaxXScore = CXScore = 0;
```

- [ ] **Step 2: Build (this alone doesn't fully validate — Task 6 must exist for these fields to be read/written; confirm the project still compiles with the new unused-until-Task-6 fields)**

```bash
cd /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer
dotnet build Overlayer/Overlayer.csproj -c Release --nologo
```
Expected: `오류 0개`.

**Steps 3-4 below run after Task 6 is complete** (they call `AccuracyStats__CalculatePercentAcc.ApplyVersionIndependentStats`, which Task 6 defines).

- [ ] **Step 3: Write the weight-table verification harness**

Create `/tmp/mvtest/Task7.cs`. It drives `Hit`'s own counters the same way `Hit__GetHitMarginInDeg.Prefix` would for a scripted sequence (3 XPerfect, 2 EarlyPerfect, 1 VeryEarly hits, all at `Difficulty.Strict` so they land in the `C*` counters `ApplyVersionIndependentStats` reads), then calls `ApplyVersionIndependentStats` directly with a freshly-constructed `scrMarginTracker` (its game-override-state counts — Auto/Midspin/Multipress/FailMiss/FailOverload/FailedFloor — are all 0 on a fresh instance, isolating this check to exactly the self-computed-counter part of the formula), and compares the result against alpha's own weight table computed independently in the harness:
```csharp
using System;
using System.IO;
using System.Reflection;

static class Task7 {
    static int Main(string[] args) {
        var managed = args[0];
        var overlayerDll = args[1];
        var buildLabel = args[2];
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) => {
            var n = new AssemblyName(e.Name).Name + ".dll";
            var p = Path.Combine(managed, n);
            return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
        var game = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
        var ov = Assembly.LoadFrom(overlayerDll);
        var hitType = ov.GetType("Overlayer.Tags.Hit");
        var accType = ov.GetType("Overlayer.Tags.AccuracyStats");
        var scoresType = ov.GetType("Overlayer.Tags.Scores");
        var hitMarginType = game.GetType("HitMargin");
        var difficultyType = game.GetType("Difficulty");

        var increaseCount = hitType.GetMethod("IncreaseCount", BindingFlags.Public | BindingFlags.Static);
        var increaseCCount = hitType.GetMethod("IncreaseCCount", BindingFlags.Public | BindingFlags.Static);
        var xperfect = Enum.Parse(hitMarginType, "XPerfect");
        var earlyPerfect = Enum.Parse(hitMarginType, "EarlyPerfect");
        var veryEarly = Enum.Parse(hitMarginType, "VeryEarly");
        var strict = Enum.Parse(difficultyType, "Strict");
        void HitN(object margin, int times) {
            for (int i = 0; i < times; i++) {
                increaseCount.Invoke(null, new object[] { strict, margin });
                increaseCCount.Invoke(null, new object[] { margin });
            }
        }
        HitN(xperfect, 3);
        HitN(earlyPerfect, 2);
        HitN(veryEarly, 1);

        var patchType = ov.GetType("Overlayer.Tags.Patches.P_scrMistakeManager")
            .GetNestedType("AccuracyStats__CalculatePercentAcc", BindingFlags.Public | BindingFlags.NonPublic);
        var apply = patchType.GetMethod("ApplyVersionIndependentStats", BindingFlags.Public | BindingFlags.Static);
        var tracker = Activator.CreateInstance(game.GetType("scrMarginTracker"));
        apply.Invoke(null, new object[] { tracker });

        int fails = 0;
        void Expect(string label, double got, double expected) {
            bool ok = Math.Abs(got - expected) < 0.0001;
            Console.WriteLine((ok ? "PASS " : "FAIL ") + $"{buildLabel}: {label} == {expected} (got {got})");
            if (!ok) fails++;
        }

        // PlayerHitMarginWeights: XPerfect=1.0, EarlyPerfect=0.75, VeryEarly=0.4, denominator = 6 hits.
        Expect("CXAccuracy", (double)accType.GetField("CXAccuracy").GetValue(null), 100.0 * ((3 * 1.0 + 2 * 0.75 + 1 * 0.4) / 6.0));
        // PerfectHitMargins+SemiPerfectHitMargins: all 6 hits count as success (XPerfect and EarlyPerfect
        // are both "full success" for CAccuracy; VeryEarly is not), + the 0.0001 perfect-count bonus.
        double expectedAccRatio = 5.0 / 6.0; // XPerfect(3) + EarlyPerfect(2) count; VeryEarly(1) doesn't
        double expectedAccBonus = 3 * 0.0001; // perfectFamily = 3 XPerfect hits
        Expect("CAccuracy", (double)accType.GetField("CAccuracy").GetValue(null), 100.0 * (expectedAccRatio + expectedAccBonus));
        // HitMarginXScores: XPerfect=2, everything else here=0.
        Expect("CXScore", (int)scoresType.GetField("CXScore").GetValue(null), 3 * 2);

        Console.WriteLine(fails == 0 ? "ALL PASS" : $"{fails} FAILED");
        return fails;
    }
}
```

- [ ] **Step 4: Run it — must produce the identical numbers on every build**

```bash
mcs /tmp/mvtest/Task7.cs -out:/tmp/mvtest/Task7.exe
for spec in "150:/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" "148:$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" "145:$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed"; do
  v=${spec%%:*}; m=${spec#*:}
  mono /tmp/mvtest/Task7.exe "$m" /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer/Overlayer/bin/Release/Overlayer.dll "$v"
done
```
Expected: `ALL PASS` on all three, with the exact same `CXAccuracy`/`CAccuracy`/`CXScore` values each time — this is what proves the weight tables were transcribed correctly (Review Focus item 5) and that the result is genuinely version-independent, not accidentally alpha-only.

- [ ] **Step 5: Commit**

```bash
git add Overlayer/Tags/AccuracyStats.cs Overlayer/Tags/Scores.cs Overlayer/Tags/Patches/P_scrMistakeManager.cs
git commit -m "Add CAccuracy/CXAccuracy/CXScore tags

Extract their computation into ApplyVersionIndependentStats so it's
directly testable without a live scrController.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 8: Manual in-game verification and wrap-up

**Files:** none (verification only).

- [ ] **Step 1: Full-solution build check**

```bash
cd /run/media/qwru0905/SAMSUNG/01_Code/10_CSharp/Overlayer
dotnet build Overlayer.sln -c Release --nologo
```
Expected: `오류 0개`.

- [ ] **Step 2: Full validator sweep on all three builds one more time, end to end**

```bash
H=/tmp/mvtest/Harness.exe   # or wherever Task 5's harness lives
for spec in "/run/media/qwru0905/SAMSUNG/98_HIDDEN/01_Game/04_SteamLinux/steamapps/common/A Dance of Fire and Ice/ADanceOfFireAndIce_Data/Managed" "$HOME/adofai-versions/general/A Dance of Fire and Ice_Data/Managed" "$HOME/adofai-versions/older/A Dance of Fire and Ice_Data/Managed"; do
  mono "$H" "$spec" Overlayer/bin/Release/Overlayer.dll
done
```
Expected: `0 with issues` for all three.

- [ ] **Step 3: Manual in-game check — ask the user to verify, since no automated test here substitutes for a live game**

This step cannot be automated in this environment (no headless ADOFAI runner). Report to the user (or, if running non-interactively, note in the final summary) that the following still needs a live check on the `older` or `general` Steam branch before release:
- Hit text (`{LHit}`/`{CHit}` etc.) shows sensible English/localized text for every judgment, including an XPerfect-timing hit (should show whatever "Perfect" text the pre-150 build has, per Task 4's fallback).
- `{OTE}`..`{OPL}` show nonzero, sensible numbers after a real level with a mix of judgments (not just the "always 0 outside a game" case Task 3's harness covers).
- `{Accuracy}`/`{XAccuracy}` match what the game's own results screen shows.
- `{CAccuracy}`/`{CXAccuracy}`/`{CXScore}` show plausible numbers and don't throw/log errors (check the mod's log for `[LazyPatch]`/`Error` lines after a play session).
- The mod loads without a `LazyPatch ... Not Compatible`/validation error flood in the log on that branch.

- [ ] **Step 4: Update the spec's status line**

In `docs/superpowers/specs/2026-09-29-multi-version-support-design.md`, change:
```
Status: draft, pending review.
```
to:
```
Status: implemented on `feat/multi-version-support`, pending in-game verification (see plan Task 8).
```

- [ ] **Step 5: Commit**

```bash
git add docs/superpowers/specs/2026-09-29-multi-version-support-design.md
git commit -m "Mark multi-version-support spec as implemented, pending in-game check

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```
