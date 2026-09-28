# Multi-version support (r145/r148/r150) — design

Status: draft, pending review.
Branch: `feat/multi-version-support` (based on `v3`).

## Goal

One `Overlayer.dll` that works correctly on ADOFAI release 150 (alpha,
current target), 148 (`general`, the public branch) and 145 (`older`).
`older-2.9.8` (release 136, Unity 2022.3, missing several game types) is
explicitly out of scope for this change; see "Out of scope" below.

## Background: why this is not just "add a version check"

Overlayer is compiled against the alpha `Assembly-CSharp.dll`. C# bakes an
enum member reference (`HitMargin.XPerfect`) into IL as a literal integer
at compile time — the member's *name* is not looked up at runtime unless
code explicitly asks for it (`.ToString()`, `Enum.GetName`, `Enum.Parse`,
`nameof` is compile-time only). Across the three builds, the game's
`HitMargin` enum has different members at different numeric values (see
[[adofai-version-matrix]] in memory, and the raw diff in
`/tmp/.../apidiff_out2.txt` from this session if still present):

| member | alpha (150) | general/older (148/145) |
|---|---|---|
| PerfectMinus / XPerfect / PerfectPlus | 3 / 4 / 5 | *(members don't exist — the game only has `Perfect` = 3)* |
| LatePerfect | 6 | 4 |
| VeryLate | 7 | 5 |
| TooLate | 8 | 6 |
| Multipress | 9 | 7 |
| FailMiss | 10 | 8 |
| FailOverload | 11 | 9 |
| Auto | 12 | 10 |
| OverPress | 13 | 11 |
| Midspin / FailedFloor | 14 / 15 | *(don't exist)* |

Measured empirically: on the earlier `general`/`older` DLLs, all 46
`LazyPatch` targets otherwise resolve, and every Unity/game API Overlayer
calls exists there too. The only breakage is (a) the old
`scrMisc.GetHitMargin` signature, and (b) this enum numbering diff, and
only at the small set of places listed below.

### Where the numbering actually matters (and where it doesn't)

Most of the ~100 `HitMargin` references in `Hit.cs`/`Scores.cs`/
`ComboStats.cs` are **self-consistent**: `Hit.GetHitMarginInDeg` computes
a `HitMargin` from angle math and returns an alpha-compiled literal (e.g.
`HitMargin.XPerfect`, IL value 4); that value is stored in `Hit.Lenient`
etc. and later compared with `switch`/`case HitMargin.XPerfect` — also an
alpha-compiled literal 4 in the *same* assembly. Two literals baked at the
same compile time by the same compiler always agree, independent of which
game DLL is actually loaded. No fix needed there.

The break only happens where Overlayer's value crosses into something
whose numbering is fixed by the *currently loaded* game assembly instead
of Overlayer's own compiled literals:

1. **Display text** — `Hit.LHit/NHit/SHit/CHit` build a localization key
   with `"HitMargin." + Lenient`. String-concatenating an enum calls
   `.ToString()`, which asks the CLR to look up the value in the
   **runtime-loaded** `HitMargin` type's name table — not Overlayer's
   compiled intent. On 148, value 4 is named `LatePerfect`, so an
   XPerfect hit (Overlayer's own classification) would display the
   `LatePerfect` string. **4 sites.**
2. **Reading the game's own count array** —
   `hitMarginsCount[(int)HitMargin.LatePerfect]` casts Overlayer's
   compiled literal (6) to index an array the *game* filled using the
   *game's* runtime numbering (where LatePerfect is 4 on r148) — wrong
   slot. **10 sites** (`Hit.cs` `OTE`..`OPL`).
3. **Calling into the game with a literal** —
   `tracker.GetHits(HitMargin.FailMiss)` passes Overlayer's compiled
   literal as an argument the game interprets using its own numbering.
   **2 sites** in `Hit.cs` (`MissCount`, `Overloads`) + **3 call sites**
   in `P_scrMistakeManager.cs` (`GetHits(PerfectMinus, XPerfect,
   PerfectPlus, Auto, Midspin, Multipress)`, `GetHits(EarlyPerfect,
   LatePerfect)`, `GetHits(FailedFloor)` — the last two also reference
   `PerfectMinus`/`XPerfect`/`PerfectPlus`/`Midspin`/`FailedFloor`, which
   don't exist as members pre-150 at all).
4. **`scrMisc.GetHitMargin`** — removed in 150, replaced by
   `GetHitMarginInDeg`/`GetHitMarginInSec` (already patched; see
   `P_scrMisc.cs`). Pre-150 still has only the old signature.

Everything else (the self-computed classification, `ComboStats`'s
`Combos[(int)difficulty][(int)margin]`, `Scores.cs`'s switches) needs no
change: it is Overlayer's own literals in, Overlayer's own literals out.

## Design

### 1. Version detection (reuse, don't duplicate)

`LazyPatchAttribute.CurrentVersion` already reads
`Releases.releaseNumber` via reflection. Expose it as a small public
`GameVersion.Current` (int, -1 if `Releases` doesn't exist — which also
covers `older-2.9.8` gracefully instead of throwing; see "Out of scope").
`LazyPatchAttribute` is changed to use this shared lookup instead of
inlining its own.

### 2. `GameMargin` helper (new file, `Tags/GameMargin.cs`)

A tiny static class with a cache (`Dictionary<string, HitMargin?>`)
resolving a `HitMargin` member **by name**, against whichever `HitMargin`
type is actually loaded, the one time-safe way to ask "what value does
this build use for X":

```csharp
public static class GameMargin {
    public static HitMargin? TryGet(string name) // null if absent on this build
    public static int? IndexOf(string name)      // (int)TryGet(name), or null
    public static HitMargin[] Existing(params string[] names) // filters absent ones
}
```

Implementation resolves via `Enum.TryParse(typeof(HitMargin), name, out
var v)` (reflection-based, so it reads the *loaded* type's own name
table — the opposite of a compiled literal) and caches results, since
this runs on a hot path (`OTE`, etc. are read every frame by active
overlays).

Fixes applied with this helper:
- `hitMarginsCount[(int)HitMargin.X]` → `hitMarginsCount[GameMargin.IndexOf("X") ?? -1]`
  guarded to return 0 when the index is -1 (member doesn't exist on this
  build). This directly implements the approved policy: **`OPM`/`OXP`/`OPL`
  report 0 pre-150** (their game members don't exist), while the
  self-computed `LPM`/`LXP`/`LPP`/... tags are untouched and keep working.
- `MissCount`/`Overloads` → resolve `"FailMiss"`/`"FailOverload"` through
  `GameMargin.TryGet` before calling `GetHits`.
- `P_scrMistakeManager`'s three `GetHits(...)` calls → build the argument
  array via `GameMargin.Existing(...)`, so a pre-150 call simply omits
  `PerfectMinus`/`XPerfect`/`PerfectPlus`/`Midspin`/`FailedFloor` instead
  of passing a wrong/misresolved value.

### 3. Display text (`Hit.LHit`/`NHit`/`SHit`/`CHit`)

Replace `"HitMargin." + Lenient` (implicit `.ToString()`, runtime-type
hazard) with a fixed name derived from a switch over Overlayer's own
compiled literals (safe — see "self-consistent" above):

```csharp
static string NameOf(HitMargin m) => m switch {
    HitMargin.TooEarly => nameof(HitMargin.TooEarly),
    ...
    HitMargin.XPerfect => nameof(HitMargin.XPerfect),
    ...
};
```

**Open question for review:** pre-150, the game's own localization table
has no `HitMargin.XPerfect`/`PerfectMinus`/`PerfectPlus` entries (the
split didn't exist yet), only `HitMargin.Perfect`. Default behavior:
try the exact key first (`RDString.GetWithCheck`, which reports whether
the key existed), and if it doesn't exist, fall back to
`HitMargin.Perfect` for those three specific values only. Flag if a
different fallback is wanted.

### 4. `P_scrMisc.cs` — version-gated patch selection

Add `MinVersion = 150` to the existing `Hit__GetHitMarginInDeg` /
`InSec` / `Combo__...` / `Scores__...` classes (six classes total), and
add a mirrored set of classes targeting the old
`GetHitMargin(hitangle, refangle, isCW, bpmTimesSpeed, conductorPitch,
marginScale)` signature with `MaxVersion = 149`. The old classes share
the same `Apply*` helpers already in `P_scrMisc.cs`; only the
`Prefix`/`Postfix` signature and the call to `Hit.GetHitMarginInDeg`
(same self-computed classification, angle math is version-independent)
differ. This also stops `LazyPatchValidator` from reporting "not found"
for whichever pair doesn't apply to the running build — right now it
would flag 2 false positives per old build (confirmed in this session's
harness run).

### 5. `P_scrMistakeManager.cs` — accuracy, version-branched

For version >= 150: unchanged (current code, reads the game's
`percentAcc`/`percentXAcc`/`xScore`/etc. fields, which only carry
meaningful split data since 150).

For version < 150: restore the pre-XPerfect-split accuracy computation
that existed at commit `ca3c732` (git history, "Fix Overlayer for the
current ADOFAI build (Unity 6000.3.10f1 / r147)") — it computes
`Accuracy`/`XAccuracy`/`MaxAccuracy`/etc. purely from Overlayer's own
`Hit.LTE`/`LVE`/.../ counters (self-consistent, no game enum crossing at
all). `Scores.XScore`/`MaxXScore` are left at their `Reset()` default
(0) pre-150, since the game has no X-Score concept there — matches the
approved policy of degrading the game-reported values while keeping
Overlayer's own computation intact.

### 6. Testing (no way to launch the old game builds interactively here)

- Build in Release; `LazyPatchValidator`'s existing harness (see
  [[lazypatch-validation]] in memory) run against `general`/`older`'s
  `Assembly-CSharp.dll` must report 0 issues for all patches (currently
  reports the 2 false positives from §4, which this change removes).
- A small scratch console (same pattern as the validator harness) that
  loads each of the three game assemblies in turn and:
  - calls `GameMargin.TryGet` for every name used above and asserts the
    expected present/absent set per build,
  - feeds `Hit.GetHitMarginInDeg` a handful of angle/bpm combinations and
    asserts `NameOf(...)` produces the intended English key on each
    build (catches any accidental `.ToString()` reintroduction).
- Manual: the user running the mod on the `older`/`general` Steam branch
  before release, since no test here substitutes for seeing real hit
  text/combo/accuracy in-game.

## Out of scope (explicitly)

- **`older-2.9.8` (r136).** Missing `scrPlayer`, `scrPlayerManager`,
  `scrMarginTracker`, `scrHitTextManager`, and `Releases` entirely — a
  materially larger change (the mod's hit-tracking model assumes
  `scrMarginTracker` exists). `GameVersion.Current` returning -1 there at
  least stops `LazyPatchAttribute`'s static constructor from throwing, so
  the mod fails a specific patch instead of failing to load, but no
  feature work for 136 is included here.
- The in-game hit-text/particle rendering the *game itself* draws (not
  Overlayer's own overlay) — unaffected either way, out of Overlayer's
  control.
- Auto-updater changes — not needed; one DLL still serves all three
  builds, so the existing update flow (see [[fork-and-release-notes]] in
  memory) is untouched.
- A `ComboStats.Combos[(int)difficulty][(int)margin]` edge case: if a
  script calls a margin-combo tag (e.g. `LMarginCombo`) with a
  **game-runtime-resolved** `HitMargin` value on a pre-150 build (i.e.
  the scripting layer parses a name against the loaded type rather than
  using Overlayer's own literal), its numeric value could coincide with
  a different index than Overlayer's own classification uses for that
  slot (e.g. game's `LatePerfect`=4 coincides with Overlayer's own
  `XPerfect`=4). This cannot crash (the array is sized via
  `EnumHelper<HitMargin>.GetValues().Length`, always large enough) and is
  narrow enough (requires explicit scripted access via a
  game-runtime-resolved value) that it is called out as a known
  limitation rather than fixed here.

## Files touched (expected)

- `Overlayer/Core/Patches/LazyPatchAttribute.cs` — use shared version lookup.
- New: `Overlayer/Utils/GameVersion.cs` — shared version detection.
- New: `Overlayer/Tags/GameMargin.cs` — name-based margin resolution.
- `Overlayer/Tags/Hit.cs` — `LHit`/`NHit`/`SHit`/`CHit` (§3),
  `OTE`..`OPL`/`MissCount`/`Overloads` (§2).
- `Overlayer/Tags/Patches/P_scrMisc.cs` — version-gated patch pairs (§4).
- `Overlayer/Tags/Patches/P_scrMistakeManager.cs` — version-branched
  accuracy (§5).

No changes expected to `Scores.cs`, `ComboStats.cs`, the scripting layer,
or the updater.
