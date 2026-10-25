# Plan 002: Sweep many seeds asserting one general wall-piece-selection invariant, instead of one hand-written test per incident

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat dc443d1..HEAD -- Assets/Game/Systems/LevelGeneration/Editor/RoomShellAssembler.cs Assets/Game/Tests/RoomShellAssemblerCornerTagTests.cs`.
> **Read `plans/README.md`'s "Origin" section first** — this plan builds on
> Plan 001's changes, some of which are uncommitted working-tree state as
> of planning (see that section), not just commit history. Confirm Plan
> 001's status is `DONE` before starting (see "Depends on" below), which
> implies its changes are present regardless of commit state.

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: LOW
- **Depends on**: `plans/001-classify-wall-cell-junctions.md` (this plan
  asserts against the `MultiRoomSeamPrefab` classification Plan 001 adds —
  do not start this plan until Plan 001's status row in `plans/README.md`
  reads `DONE`)
- **Category**: tests
- **Planned at**: commit `dc443d1`, 2026-09-21

**Visual guide**: `C:\Users\Micha\AppData\Local\Temp\plan-002-visual-guide.html`
(the prediction that came true, before/after test shape, the invariant flowchart)

## Why this matters

`Assets/Game/Tests/RoomShellAssemblerCornerTagTests.cs`'s own comment,
written 2026-09-04, already predicted this: *"CarveNotches and
MergeOpenSpaces each have their own separate, already-deferred instance of
'an irregular room shape can produce a cell with more than one tagged
side'... stays out of scope for it."* That deferred bug class resurfaced
live, 17 days later, in a hands-on session — a human had to bake a level,
look at the Scene view, and notice the geometry was wrong. Every existing
test in this area (`RoomShellAssemblerCornerTagTests`,
`BspLayoutGeneratorCorridorTests`) guards one specific, already-diagnosed
symptom on one or a handful of specific seeds. Nothing sweeps a wide seed/
parameter space asserting a *general* correctness property, so the next
wrong-piece bug — in a shape nobody has hit yet — will surface the same
way this one did: a human pressing Bake and noticing something looks off,
then a multi-hour live debugging session to characterize it.

This plan adds that general sweep, directly targeting "problems... causing
a lot of manual edits": catch the bug class before a human ever presses
Bake.

## Current state

- `Assets/Game/Tests/BspLayoutGeneratorCorridorTests.cs:47-60` is the
  existing seed-sweep pattern to copy exactly:

  ```csharp
  private static readonly int[] Seeds = { 1, 2, 3, 42, 1000, -500, 99999 };

  [Test]
  public void CorridorFlanks_HaveNoUnboundedVoid([ValueSource(nameof(Seeds))] int seed)
  {
      var parameters = new GenerationParameters { Seed = seed, RoomCount = 8, CorridorCount = 2 };
      AssertNoUnboundedCorridorVoid(parameters, $"seed {seed}");
  }
  ```

- `Assets/Game/Tests/RoomShellAssemblerCornerTagTests.cs` (full file — read
  it, Plan 001 should have left it unmodified) is the exact "bake into a
  temp parent, load the real `WallPrefabRuleSet`, inspect
  `PrefabBrushObject.SourcePrefab`" structure this plan's test reuses.

- After Plan 001 lands, `RoomShellAssembler.cs`'s wall-placement loop
  (`BuildShells`, ~lines 126-165 post-Plan-001) has this shape for every
  wall cell:
  1. Doorway cells → a `Door` prefab (unrelated to this plan, skip these
     cells in the invariant below).
  2. `primaryRoomsByCell[cell].Count >= 2` → `config.MultiRoomSeamPrefab`
     (Plan 001's new branch).
  3. Otherwise → `PrefabRuleResolver.Resolve` against `WallPrefabRuleSet`
     (`Straight`/`Corner`/`TJunction`/`DefaultPrefab` fallback).

- `Packages/com.juvenile.games.core/Editor/Tools/PrefabBrush/Rules/PrefabRuleResolver.cs`'s
  `ResolveResult` struct has a `Matched` bool (`true` only when an actual
  rule fired, `false` when it fell through to `DefaultPrefab`) — this
  plan's invariant uses `Matched` to distinguish "confidently placed the
  wrong piece" (a hard failure) from "honestly reports no rule fit" (a
  known, logged, accepted gap for the single-room dead-end/cross cases —
  see `Assets/Game/PrefabBrush/WallPrefabRuleSet.asset`, which has no rule
  for 1-neighbor or 4-neighbor single-room patterns as of this session).

- `WallPrefabRuleSet.asset`'s current rules (verified live this session):
  `Corner` = exactly 2 adjacent neighbors tagged, opposite two explicitly
  not; `Straight` = exactly 2 opposite neighbors tagged, adjacent two
  explicitly not; `TJunction` = exactly 3 neighbors tagged, the 4th
  explicitly not. `DefaultPrefab` = the `Straight` prefab, `Matched=false`,
  for 0, 1, or 4 tagged neighbors.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Import the new test file | `unicli exec AssetDatabase.Import --path "Assets/Game/Tests/RoomShellAssemblerJunctionInvariantTests.cs" --json` | `"success": true` |
| Compile | `unicli exec Compile --json` | `"errorCount": 0` |
| Run the new tests | `unicli exec TestRunner.RunEditMode '{"testNames":["Game.Tests.RoomShellAssemblerJunctionInvariantTests"]}' --json` | all pass |
| Full regression | `unicli exec TestRunner.RunEditMode --json` | all existing tests still pass |

## Suggested executor toolkit

- `unicli:unity-development` skill for all Editor automation.

## Scope

**In scope** (the only file you should create):
- `Assets/Game/Tests/RoomShellAssemblerJunctionInvariantTests.cs` (create)

**Out of scope** (do NOT touch, even though they look related):
- Any production code under `Assets/Game/Systems/LevelGeneration/` — this
  plan is test-only. If the invariant fails on a seed, that is a real bug
  to report (see STOP conditions), not something to fix by editing
  `RoomShellAssembler.cs` from inside this plan.
- The existing `RoomShellAssemblerCornerTagTests.cs` and
  `BspLayoutGeneratorCorridorTests.cs` — read for pattern reference, do not
  modify.
- Any seed/parameter combination this plan's sweep doesn't already cover —
  do not expand the seed list beyond Step 1's list without STOPping first
  (a much larger sweep changes this test's runtime cost, which is worth a
  human decision, not a silent expansion).

## Git workflow

- Stay on the existing branch `prototype/room-generator` — see
  `SCARAB-MAP-01`'s note that all level-generation work happens there, not
  on a new branch.
- Commit message style: `[TASK]` prefix (this is test coverage, not a bug
  fix or new feature), body explains why. End with
  `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Do NOT push unless the operator instructed it.

## Steps

### Step 1: Pick the seed/parameter sweep

Create `Assets/Game/Tests/RoomShellAssemblerJunctionInvariantTests.cs` in
namespace `Game.Tests`. Define the sweep exactly like
`BspLayoutGeneratorCorridorTests.Seeds`, but include the seed this plan (and
Plan 001) were verified against, plus a spread of others:

```csharp
private static readonly int[] Seeds = { 1959606433, 1, 2, 3, 42, 1000, -500, 99999, 777 };
```

Use one fixed parameter set across all seeds (deliberately not sweeping
parameters too — that's a much larger combinatorial space; a follow-up can
widen this once this plan's shape is proven):

```csharp
private static GenerationParameters MakeParameters(int seed) => new()
{
    Seed = seed, RoomCount = 8, PuzzleRoomCount = 2, OpenSpaceCount = 1,
    MinEntryExitDistance = 3, LoopCount = 2, CorridorCount = 2,
    CorridorMinLength = 4, CorridorMaxLength = 8, IrregularRoomCount = 2,
    MinLeafSize = 3, SpareDoorCount = 0, IsolatedPocketCount = 0,
};
```

### Step 2: Write the invariant test

Model the bake-into-temp-parent setup exactly on
`RoomShellAssemblerCornerTagTests.cs` (load `WallPrefabRuleSet.asset` from
`Assets/Game/PrefabBrush/WallPrefabRuleSet.asset`, and also load
`Assets/Game/World/Prefabs/Environment/JunctionWithCorner.prefab` as
`MultiRoomSeamPrefab` on the `ShellAssemblyConfig`, per Plan 001).

For each seed, after calling `RoomShellAssembler.BuildShells`, recompute
`primaryRoomsByCell`-equivalent membership independently inside the test
(do not call `RoomShellAssembler`'s private methods reflectively — recompute
from `layout.Rooms[*].FootprintCells` the same way
`RoomShellAssemblerCornerTagTests` recomputes `cellOwner`, and the same way
the 8-way/4-way membership was recomputed live this session: for every wall
cell, 4-way-check each neighbor direction for whether it's itself a wall
cell whose room-membership overlaps this cell's own). Then assert, for
every wall cell that is **not** a doorway cell:

- **If** this cell's own room-membership set has 2+ distinct room/margin
  ids: the placed instance's `PrefabBrushObject.SourcePrefab.name` must
  equal `"JunctionWithCorner"`.
- **Else** (single-room membership), call
  `Core.Editor.Tools.PrefabBrush.Rules.PrefabRuleResolver.Resolve` yourself
  against a snapshot built the same way `RoomShellAssembler.BuildSnapshot`
  builds one (tag = `ruleSet.RuleTag` when a same-direction neighbor's
  membership overlaps, else `null`), and assert:
  - If `result.Matched == true`: the placed instance's
    `PrefabBrushObject.SourcePrefab` must be the exact same prefab as
    `result.Prefab` (i.e., what actually got placed matches what the
    resolver said to place — this catches drift between `BuildShells`'
    live behavior and a re-derived expectation, not just "some piece got
    placed").
  - If `result.Matched == false`: do not fail the test (this is the
    documented, accepted single-room dead-end/cross gap) — but count it,
    and assert the total count of `Matched == false` cells across the
    whole sweep is reported in the test's own failure message context (via
    `TestContext.WriteLine`, not `Assert`) so a human reviewing a future
    failure has that number without re-deriving it.

Use `[ValueSource(nameof(Seeds))]` exactly like
`BspLayoutGeneratorCorridorTests.CorridorFlanks_HaveNoUnboundedVoid`, so
each seed reports as its own test result.

**Verify**:
```bash
unicli exec AssetDatabase.Import --path "Assets/Game/Tests/RoomShellAssemblerJunctionInvariantTests.cs" --json
unicli exec Compile --json
unicli exec TestRunner.RunEditMode '{"testNames":["Game.Tests.RoomShellAssemblerJunctionInvariantTests"]}' --json
```
→ compile clean, all 9 seed cases pass.

### Step 3: Full regression pass

**Verify**: `unicli exec TestRunner.RunEditMode --json` → every existing
test still passes.

## Test plan

- This plan's entire content IS the test plan — see Steps 1-2. No
  production code changes to cover.
- Verification: `unicli exec TestRunner.RunEditMode --json` → all pass,
  including 9 new parameterized cases from
  `RoomShellAssemblerJunctionInvariantTests`.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `unicli exec Compile --json` → `"errorCount": 0`
- [ ] `unicli exec TestRunner.RunEditMode --json` → all pass, including 9
      cases of `RoomShellAssemblerJunctionInvariantTests`
- [ ] `git status` shows only
      `Assets/Game/Tests/RoomShellAssemblerJunctionInvariantTests.cs` as
      new/modified
- [ ] `plans/README.md` status row for `002` updated to `DONE`

## STOP conditions

Stop and report back (do not improvise) if:

- Plan 001's status row in `plans/README.md` is not `DONE` when you start —
  this plan's invariant is meaningless without Plan 001's
  `MultiRoomSeamPrefab` branch existing.
- Any seed in the sweep fails the invariant — this is a **real finding**,
  not a flaky test to retry. Report which seed, which cell, and whether it
  was a multi-room-seam mismatch or a single-room resolver-drift mismatch.
  Do not weaken the assertion or drop the failing seed from the list to
  make the test pass.
- The code at the cited locations in "Current state" doesn't match what you
  find after Plan 001 landed (Plan 001 may have implemented the branch
  differently than sketched there — read the actual `BuildShells` shape
  before assuming this plan's re-derivation logic in Step 2 is wrong).

## Maintenance notes

- This test's runtime scales with seed count × room count; if it becomes
  slow enough to discourage running locally, that's a signal to move it to
  a `[Category("Slow")]` or similar split, not to shrink the seed list
  silently.
- A future plan could widen this to sweep parameter combinations too
  (`IrregularRoomCount`, `CorridorCount`, etc.), once this plan's single-
  parameter-set sweep is proven stable — flagged here as explicitly
  deferred, not forgotten.
- A reviewer should scrutinize: that Step 2's independent re-derivation of
  `primaryRoomsByCell` genuinely mirrors `RoomShellAssembler`'s own
  algorithm (8-way fallback for pure-diagonal corners, 4-way priority
  otherwise) — a re-derivation that's subtly wrong would make this test
  assert the wrong thing confidently, which is worse than no test at all.
