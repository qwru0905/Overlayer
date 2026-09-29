# Multi-version support (r145/r148/r150) — design

Status: draft, pending review.
Branch: `feat/multi-version-support` (based on `v3`, rebased after
`v3`@`1a60ee7` — see "Related fix already on `v3`" below).

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

### Related fix already on `v3` (not part of this change, but now in its history)

While investigating this, found that `Hit.FixMargin` forces an
autoplay/midspin hit to `HitMargin.XPerfect` (so combo-continuation
treats it as a perfect, matching the game's own
`HitMarginHelper.PerfectHitMargins`), but that same forced value was also
flowing into the XP/etc. counts, `Scores.Score`, and `MarginCombo` —
none of which the game itself credits toward *those specific* stats
(see the `XAccuracy`/`X-Score` weight tables in §5 below: they have no
entry for Auto/Midspin at all). Fixed on `v3` at `1a60ee7`
(`Hit.IsAutoOrMidspin`, checked in `P_scrMisc.cs` before
counting/scoring/margin-combo). This branch is rebased on top of it. It's
unrelated to multi-version support (the bug exists on alpha too,
independent of which build is running) and needs no special per-version
handling here — `IsAutoOrMidspin` only checks booleans
(`midspinInfiniteMargin`, `RDC.auto`), not enum numbering.

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

**Midspin vs. autoplay, specifically:** pre-150 game code has no
`Midspin` member at all — verified in the decompiled 148 source, a
midspin-forced hit and a true autoplay hit are *both* classified as the
single `HitMargin.Auto` by the game itself (`hitMargin2 = HitMargin.Auto`
fires for either `player.midspinInfiniteMargin` or `player.auto`). So
`GameMargin.Existing("PerfectMinus", "XPerfect", "PerfectPlus", "Auto",
"Midspin", "Multipress")` on 148/145 naturally reduces to `[Auto,
Multipress]` — dropping `"Midspin"` loses nothing, because the game's own
`hitMarginsCount[Auto]` on those builds *already* holds both the true
autoplay and the midspin hits pre-merged. No special-casing needed beyond
the existing-name filter.

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

**Decided:** pre-150, the game's own localization table has no
`HitMargin.XPerfect`/`PerfectMinus`/`PerfectPlus` entries (the split
didn't exist yet), only `HitMargin.Perfect`. `Hit.LHit`/`NHit`/`SHit`/
`CHit` are the *only* tags affected (they're the only place a `HitMargin`
value is turned into localized display text; `RDString.Get("HitMargin."
+ ...)` is not used anywhere else). Behavior: try the exact key first via
`RDString.GetWithCheck` (which reports whether the key existed); only
when the result is `PerfectMinus`, `XPerfect`, or `PerfectPlus` *and* the
key doesn't exist, fall back to the `HitMargin.Perfect` key instead. The
other six values (`TooEarly`/`VeryEarly`/`EarlyPerfect`/`LatePerfect`/
`VeryLate`/`TooLate`) have matching keys on every build already and never
hit the fallback path.

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

### 5. Accuracy/X-Score — two families, not a version branch

Re-examined this directly in the decompiled 148/145 source instead of
assuming the r147-era restore would still apply, and it doesn't: the
picture is different from (and simpler than) the original plan above.

**`scrMarginTracker.percentAcc`/`percentXAcc`/`xScore` exist as real,
populated fields on every one of 145/148/150** (confirmed by reading
`CalculatePercentAcc()` in the 148 decompile directly, not just the
member-existence diff). So `AccuracyStats.Accuracy`/`MaxAccuracy` and
`Scores.XScore`/`MaxXScore` in `P_scrMistakeManager.cs` can keep reading
those fields **unconditionally on every version, no `MinVersion` branch,
no restored old formula** — simpler than §5 originally proposed.

But the two fields disagree on whether an autoplay/midspin hit counts as
a success, and *differently* depending on which field:

| | alpha (150) `percentAcc` | alpha `percentXAcc`/`xScore` | 148/145 `percentAcc` **and** `percentXAcc` |
|---|---|---|---|
| formula | `GetHits(PerfectHitMargins) + GetHits(SemiPerfectHitMargins)` over `hitMargins.Count + GetDeaths()` | `GetHitsWithWeights(PlayerHitMarginTypes)`, weights below, over `playerHitMarginCount` (excludes non-listed margins entirely) | `GetHits(Perfect, EarlyPerfect, LatePerfect, Auto)` (Acc) / same idea with hand-written weights (XAcc), both over `hitMargins.Count` |
| Auto/Midspin | **counted as full success** (`PerfectHitMargins` includes `Auto`/`Midspin`/`Multipress`) | **weight 0, excluded from the denominator too** (`PlayerHitMarginWeights` has no entry for `Auto`/`Midspin`/`Multipress`/`OverPress`) | **counted as full-weight success** (explicitly named in the formula) |

So: alpha's plain `Accuracy` already agrees with old versions (Auto =
success) — nothing to fix there. Only alpha's `XAccuracy`/`X-Score`
introduced the "Auto doesn't count" idea, and old versions' own
`percentXAcc`/`xScore` never adopted it (that distinction didn't exist
before the XPerfect split). This is exactly what was flagged as
TUF-preferred: a version-consistent X-Accuracy/X-Score that always
excludes Auto/Midspin, rather than one whose meaning silently changes
depending on which build produced it.

**Decided: keep two families, both always available, no version branch:**

1. **`AccuracyStats.Accuracy`/`MaxAccuracy`/`XAccuracy`/`AbsXAccuracy`/
   `AbsMaxXAccuracy` and `Scores.XScore`/`MaxXScore` — unchanged.**
   Read the game's own `percentAcc`/`percentXAcc`/`xScore`/`maxXScore`
   fields exactly as today, on every version, no branch. Their meaning
   is "whatever this specific game build's own native accuracy/X-Score
   is" — on old builds, that includes Auto by that build's own design.
2. **A new, version-independent family that always applies alpha's
   exclusion weights**, computed entirely from Overlayer's own
   self-computed per-difficulty counters (`Hit.LTE`/`LVE`/`LEP`/`LPM`/
   `LXP`/`LPP`/`LLP`/`LVL`/`LTL`, already correct on every version per
   the "self-consistent" analysis above — no `GameMargin` lookup needed
   for these), weighted with the exact `HitMarginHelper.
   PlayerHitMarginWeights` table (`XPerfect`/`PerfectMinus`/
   `PerfectPlus`=1.0, `EarlyPerfect`/`LatePerfect`=0.75, `VeryEarly`/
   `VeryLate`=0.4, `TooEarly`/`TooLate`=0.2, `FailedFloor`=0.2,
   `FailMiss`/`FailOverload`=0.0), copied verbatim so the number matches
   alpha's `XAccuracy` bit-for-bit when actually running on alpha.
   `FailedFloor`/`FailMiss`/`FailOverload` aren't part of Overlayer's own
   classification (they're game override states), so this family reads
   them via `GameMargin` (§2) — `FailedFloor` resolves to nothing pre-150
   and is simply omitted, same reasoning as the `Midspin` case in §2.
   **Decided naming:** `C`-prefixed, matching the existing convention for
   Overlayer's own combined/self-computed value (`Hit.CHit`/`CTE`/...):
   `AccuracyStats.CAccuracy`, `AccuracyStats.CXAccuracy`,
   `Scores.CXScore`. Only the single combined value for now — no
   per-difficulty (`L`/`N`/`S`) variants yet; revisit if/when they're
   actually wanted.

This is a real scope addition beyond "make 145/148 not crash" (a second,
always-on accuracy system) — called out explicitly in case it should be
split into its own follow-up instead of landing in this branch.

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
    build (catches any accidental `.ToString()` reintroduction),
  - confirms `scrMarginTracker` actually exposes `percentAcc`/
    `percentXAcc`/`xScore`/`maxXScore` as readable members on all three
    builds (§5 relies on this instead of a version branch — should be
    caught by `LazyPatchValidator` already for anything Harmony patches,
    but these are plain field/property reads, not patch targets, so this
    check is the only thing that would catch it),
  - computes `CAccuracy`/`CXAccuracy` from a scripted sequence of hits on
    the 150 build and asserts it matches the real `AccuracyStats.
    XAccuracy` for that same sequence (proves the copied weight table is
    correct), then runs the same sequence through the 148/145 assemblies
    and asserts `CXAccuracy` stays identical while `AccuracyStats.
    XAccuracy` (game-native) differs by including Auto.
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
- `Overlayer/Tags/Patches/P_scrMistakeManager.cs` — compute the new
  `CAccuracy`/`CXAccuracy`/`CXScore` family alongside the existing
  (unchanged) game-native reads (§5).
- `Overlayer/Tags/AccuracyStats.cs`, `Overlayer/Tags/Scores.cs` — new
  `C`-prefixed tags (§5). Existing tags in both files are unchanged.

No changes expected to `ComboStats.cs`, the scripting layer, or the
updater.
