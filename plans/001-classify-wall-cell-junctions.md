# Plan 001: Classify multi-room wall-cell seams before piece selection, instead of collapsing them into the same lossy 4-tag pattern as real single-room junctions

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat dc443d1..HEAD -- Assets/Game/Systems/LevelGeneration/Editor/RoomShellAssembler.cs Assets/Game/Systems/LevelGeneration/Editor/LevelGenerationWindow.cs Assets/Game/PrefabBrush/WallPrefabRuleSet.asset`.
> **This plan also depends on uncommitted working-tree state — read
> `plans/README.md`'s "Origin" section before starting.** HEAD is `dc443d1`
> with no new commits, but `WallPrefabRuleSet.asset` and
> `JunctionWithCorner.prefab` were left modified/untracked, uncommitted, at
> planning time — the diff above will show nothing if they get reverted.
> Also run `git status --short -- Assets/Game/PrefabBrush/WallPrefabRuleSet.asset Assets/Game/World/Prefabs/Environment/JunctionWithCorner.prefab`
> and confirm both still show as modified/untracked. If any in-scope file
> changed, or the uncommitted files are missing/reverted, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: M
- **Risk**: MED
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `dc443d1`, 2026-09-21

**Visual guide**: `C:\Users\Micha\AppData\Local\Temp\plan-001-visual-guide.html`
(diagrams of the seam, the cell-14,-1 example, and the before/after branch shape)

## Why this matters

`RoomShellAssembler.BuildShells` places one wall/corner/door piece per wall
cell by asking `PrefabRuleResolver.Resolve` to match a flat 4-direction
`This`/`NotThis` tag pattern (`Core.Tools.PrefabBrush.NeighborCondition`).
That tag is a lossy boolean collapse of richer data `RoomShellAssembler`
already computed for itself (`primaryRoomsByCell`: which *specific* room(s)
border this wall cell). The collapse throws away room identity/cardinality —
so a wall cell that borders **two different rooms directly** (their corners
landing on the same or adjacent cells) looks *identical* to the resolver as a
wall cell that's a genuine mid-run branch off **one** room's own boundary.

This was verified live against a real generated seed this session
(`1959606433`, params below): of 10 wall cells that matched a `TJunction`
rule added this session, **8 actually bordered two distinct rooms/margins
directly** (`primaryRoomsByCell[cell].Count == 2`) — not a real branch at
all, just two rooms' corners colliding across a narrow shared gap column.
Only 2 were genuine single-room cases. Placing the same T-shaped piece on
both classes of cell produced visibly wrong geometry (confirmed in the
Unity Editor Scene view) — this is exactly the class of bug behind
"consistent clean generation causing a lot of manual edits." After this
plan, the two classes get routed to different, correct pieces.

## Current state

- `Assets/Game/Systems/LevelGeneration/Editor/RoomShellAssembler.cs` —
  computes wall-cell topology and places pieces. The relevant pieces:

  `primaryRoomsByCell` (lines 107-111) is already a `Dictionary<Vector2Int,
  HashSet<string>>` mapping each wall cell to the **set** of distinct room
  ids it borders — this is the data that gets thrown away:

  ```csharp
  // lines 107-111
  var primaryRoomsByCell = new Dictionary<Vector2Int, HashSet<string>>();
  foreach (var cell in wallCells)
      primaryRoomsByCell[cell] = directRoomsByCell.TryGetValue(cell, out var direct) && direct.Count > 0
          ? direct
          : wallCellRooms[cell];
  ```

  The main placement loop (lines 126-153) calls `BuildSnapshot` then
  `PrefabRuleResolver.Resolve` for every wall cell, with no branch for
  cardinality:

  ```csharp
  // lines 126-153 (abridged)
  foreach (var cell in wallCells)
  {
      var snapshot = BuildSnapshot(cell, primaryRoomsByCell, directions, config.RuleSet.RuleTag);
      var result = PrefabRuleResolver.Resolve(config.RuleSet, snapshot);
      if (result.Prefab == null) continue;

      if (doorwayByCell.TryGetValue(cell, out var connection))
      { /* door placement, unrelated to this plan */ continue; }

      var instance = PlacePiece(result.Prefab, cell, result.YRotation, config, parent);
      ...
  }
  ```

  `BuildSnapshot` (lines 343-361) is where the collapse happens — this is
  the seam this plan changes:

  ```csharp
  // lines 343-361
  private static NeighborSnapshot BuildSnapshot(Vector2Int cell, Dictionary<Vector2Int, HashSet<string>> primaryRoomsByCell,
      Vector2Int[] directions, string ruleTag)
  {
      var ownRooms = primaryRoomsByCell[cell];
      var snapshot = new NeighborSnapshot(directions.Length);
      for (int i = 0; i < directions.Length; i++)
      {
          var neighbor = cell + directions[i];
          snapshot.Tags[i] = primaryRoomsByCell.TryGetValue(neighbor, out var neighborRooms) &&
                              neighborRooms.Overlaps(ownRooms)
              ? ruleTag
              : null;
      }
      return snapshot;
  }
  ```

  `FourWayDirs` (lines 28-31) is the direction order used throughout
  (`N=0, E=1, S=2, W=3` — matches `Core.Tools.PrefabBrush.PrefabRule`'s own
  documented convention).

- `Assets/Game/Systems/LevelGeneration/Editor/ShellAssemblyConfig.cs` (or
  wherever `ShellAssemblyConfig` is declared — grep it, it's referenced from
  `RoomShellAssembler.cs` and `LevelGenerationWindow.cs`) currently carries
  `RuleSet`, `DoorPrefab`, `FloorMaterial`, `FloorUvMode`, `FloorThickness`,
  `CellSize`. This plan adds one field: `MultiRoomSeamPrefab`.

- `Assets/Game/Systems/LevelGeneration/Editor/LevelGenerationWindow.cs` —
  the `[MenuItem("Tools/Level Generation/Generate Level")]` window. Follow
  the existing pattern for a new optional prefab field exactly as
  `_doorPrefab` is wired (declaration, `OnGUI` `ObjectField`, `SavePendingState`/
  `LoadPendingState` EditorPrefs round-trip, and pass-through into
  `ShellAssemblyConfig` construction in `BakeLevel()`):

  ```csharp
  // OnGUI, existing pattern to copy (door prefab field)
  _doorPrefab = (GameObject)EditorGUILayout.ObjectField("Door Prefab", _doorPrefab, typeof(GameObject), false);
  ```

  ```csharp
  // SavePendingState, existing pattern to copy
  EditorPrefs.SetString(PrefsPrefix + "DoorPrefab", _doorPrefab != null ? AssetDatabase.GetAssetPath(_doorPrefab) : "");
  ```

  ```csharp
  // LoadPendingState, existing pattern to copy
  var doorPath = EditorPrefs.GetString(PrefsPrefix + "DoorPrefab", "");
  if (!string.IsNullOrEmpty(doorPath))
      _doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(doorPath);
  ```

- `Assets/Game/World/Prefabs/Environment/JunctionWithCorner.prefab` already
  exists — Michael built it this session as a proof-of-concept for the
  correct visual shape at a two-room-seam cell (3× `WallStraightPrefab` +
  1× `WallCornerPrefab_Grid` arranged as a T). **This plan assumes this
  prefab is the piece to place at a multi-room-seam cell.** See Step 1's
  precondition check before using it.

- `Assets/Game/PrefabBrush/WallPrefabRuleSet.asset` — already modified this
  session (Corner tightened from `DontCare` to `NotThis` on non-participating
  slots, Straight likewise, a `TJunction` rule added). **Do not touch this
  asset in this plan** — the new multi-room-seam routing in Step 2 bypasses
  `PrefabRuleResolver`/`WallPrefabRuleSet` entirely for cells it applies to,
  it does not add another rule to this asset.

- Repo convention: `Core.Tools.PrefabBrush.NeighborCondition` /
  `RuleTransform` / `PrefabRule` live in
  `Packages/com.juvenile.games.core/Tools/PrefabBrush/` (data types) and
  `Packages/com.juvenile.games.core/Editor/Tools/PrefabBrush/Rules/PrefabRuleResolver.cs`
  (matching logic) — this is Core's shared, generic rule-matching module,
  consumed as-is by `RoomShellAssembler`. This plan does **not** modify
  anything under `Packages/com.juvenile.games.core/` — the fix lives
  entirely in `RoomShellAssembler.cs`, which decides *before* calling into
  Core's generic resolver whether this cell even needs it.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Import a changed/new file | `unicli exec AssetDatabase.Import --path "<path>" --json` | `"success": true` |
| Compile | `unicli exec Compile --json` | `"errorCount": 0` |
| Run EditMode tests (this plan's new test + existing regression) | `unicli exec TestRunner.RunEditMode '{"testNames":["Game.Tests.RoomShellAssemblerMultiRoomSeamTests"]}' --json` | all pass |
| Run full EditMode suite (final check) | `unicli exec TestRunner.RunEditMode --json` | all existing tests still pass, no new failures |
| Inspect an asset live (used in Step 1's precondition check) | `unicli eval "<C# snippet>" --json` | returns expected data, no exception |

unicli talks to a running Unity Editor with this project open over a named
pipe — see the repo's `AGENTS.md` and the `unicli:unity-development` skill
if the Editor is not already open with this project loaded.

## Suggested executor toolkit

- Use the `unicli:unity-development` skill for all Editor automation in this
  plan (importing files, compiling, running tests, and the live prefab
  inspection in Step 1).

## Scope

**In scope** (the only files you should modify):
- `Assets/Game/Systems/LevelGeneration/Editor/RoomShellAssembler.cs`
- `Assets/Game/Systems/LevelGeneration/Editor/ShellAssemblyConfig.cs` (add one field)
- `Assets/Game/Systems/LevelGeneration/Editor/LevelGenerationWindow.cs` (add one field + its EditorPrefs round-trip)
- `Assets/Game/Tests/RoomShellAssemblerMultiRoomSeamTests.cs` (create)

**Out of scope** (do NOT touch, even though they look related):
- `Assets/Game/PrefabBrush/WallPrefabRuleSet.asset` — this plan bypasses it
  for multi-room-seam cells, it does not add a rule to it.
- `Assets/Game/Systems/LevelGeneration/Editor/BspLayoutGenerator.cs` — the
  door-corner-collision *generation* behavior (why two rooms' corners can
  land on the same cell in the first place) is unrelated and already
  addressed separately this session (door centering). This plan only
  changes which *piece* gets placed once that topology exists — it does not
  try to prevent the topology from occurring.
- The 2 single-room notch-peninsula cells found this session (e.g. cells
  where `primaryRoomsByCell[cell].Count == 1` but the cell still resolves
  oddly) — explicitly out of scope, a separate root cause (`TryCarveNotch`
  in `BspLayoutGenerator.cs`) not addressed here. Do not attempt to fix
  these as a "quick win."
- `Assets/Game/World/Prefabs/Environment/JunctionWithCorner.prefab`'s own
  contents/pivot — if Step 1's precondition check fails, STOP per that
  step; do not re-author the prefab yourself (that's Michael's hand-
  authoring call, not this plan's scope).

## Git workflow

- This repo does its level-generation work directly on the existing branch
  `prototype/room-generator` (see the map note in
  `G:\Mijn Drive\Notes\JuveNILE Games\Kanban\Projects\Scarab\Tasks\SCARAB-MAP-01 Procedural Room-Based Level Generation.md`:
  "All implementation work for this map... happens on git branch
  `prototype/room-generator`, not `master`"). Stay on that branch — do not
  create a new one.
- Commit message style: `[TASK]`/`[FIX]`/`[FEATURE]` prefix on the subject
  line, body explains *why*, e.g. from `git log --oneline -5`:
  `e67d887 [FIX] LevelBaker resolves the Prefab Stage's own scene, not whatever sat underneath it`.
  Use `[FIX]` for this plan's commit.
- End the commit message with `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Do NOT push unless the operator instructed it.

## Steps

### Step 1: Verify the precondition — `JunctionWithCorner.prefab` must be cell-relative, not world-absolute

Michael built `JunctionWithCorner.prefab` directly in a live test scene at
seed `1959606433`'s world coordinates. When last inspected this session, its
child GameObjects' **local** positions were large, scene-specific values
(around X=38-40, Z=29-30) — not small cell-relative offsets. Compare to
`WallTjunctionPrefab.prefab`, which is correctly cell-relative (children at
local `(0,0,0)` and `(0.25,0,0)`). `RoomShellAssembler.PlacePiece` places a
prefab by setting `instance.transform.localPosition = new Vector3(cell.x *
config.CellSize, yPosition, cell.y * config.CellSize)` — this only works
correctly if the prefab's own internal content is authored relative to its
own root at the origin.

Run:

```bash
unicli eval "$(cat <<'EOF'
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/World/Prefabs/Environment/JunctionWithCorner.prefab");
var sb = new System.Text.StringBuilder();
foreach (Transform child in prefab.transform)
    sb.AppendLine(child.name + " localPos=" + child.localPosition);
return sb.ToString();
EOF
)" --json
```

**Verify**: every reported `localPos` has X and Z components with absolute
value less than 1.0 (i.e., genuinely cell-relative, matching
`WallTjunctionPrefab.prefab`'s shape: one piece near `(0,0,0)`, one near
`(0.25,0,0)`, etc.).

**STOP condition**: if any child's local X or Z has absolute value ≥ 1.0
(i.e., it's still carrying its original scene-world coordinates), STOP and
report back — the prefab needs re-centering (open it in Prefab Mode and
zero out / rebase the child transforms relative to its own root) before
this plan can proceed, and that re-authoring is Michael's call, not
something to improvise here.

### Step 2: Add `MultiRoomSeamPrefab` to `ShellAssemblyConfig`

Open `Assets/Game/Systems/LevelGeneration/Editor/ShellAssemblyConfig.cs`
(grep for `class ShellAssemblyConfig` if the exact path differs from this
guess) and add one field alongside the existing `DoorPrefab`:

```csharp
public GameObject MultiRoomSeamPrefab;
```

**Verify**: `unicli exec Compile --json` → `"errorCount": 0`.

### Step 3: Compute multi-room-seam classification in `RoomShellAssembler`

In `RoomShellAssembler.cs`, add a small private helper right above
`BuildSnapshot` (around line 343):

```csharp
// A wall cell whose own primaryRoomsByCell membership already lists 2+
// distinct rooms/margins is never a genuine single-room branch — it's two
// (or more) different rooms' corners landing on the same or adjacent cells
// across a narrow shared gap. PrefabRuleResolver's 4-tag pattern can't
// distinguish this from a real T-junction (both look like "3 neighbors
// tagged This"), so this is decided here, before the resolver is ever
// consulted, using the room-identity data BuildSnapshot would otherwise
// discard (SCARAB-013 follow-up, 2026-09-21: verified live against seed
// 1959606433 — 8 of 10 misclassified cells were this case).
private static bool TryComputeMultiRoomSeamRotation(Vector2Int cell,
    Dictionary<Vector2Int, HashSet<string>> primaryRoomsByCell, Vector2Int[] fourWayDirs, out float yRotation)
{
    yRotation = 0f;
    if (!primaryRoomsByCell.TryGetValue(cell, out var ownRooms) || ownRooms.Count < 2)
        return false;

    // Find the two 4-way directions whose neighbor is itself a wall cell
    // continuing this same multi-room membership (mirrors BuildSnapshot's
    // own Overlaps check, but on the un-collapsed data) — JunctionWithCorner
    // is built for one fixed relative orientation; this picks the rotation
    // (0/90/180/270) that makes its "arm" side land on the correct one of
    // the two continuing directions. Falls back to 0f (no rotation applied)
    // if fewer or more than exactly 2 directions continue, which should not
    // happen for a wall cell (every wall cell has exactly one "open"
    // 4-way side in a 1-cell-wide gap) — treated as a data assumption, not
    // silently guessed at.
    var continuing = new List<int>();
    for (int i = 0; i < fourWayDirs.Length; i++)
    {
        var neighbor = cell + fourWayDirs[i];
        if (primaryRoomsByCell.TryGetValue(neighbor, out var neighborRooms) && neighborRooms.Overlaps(ownRooms))
            continuing.Add(i);
    }

    if (continuing.Count != 2) return false;

    // continuing[0]/[1] are indices into fourWayDirs (N=0,E=1,S=2,W=3).
    // JunctionWithCorner's authored shape (verified Step 1): straight run
    // along one axis + one corner arm. Match rotation so the arm points at
    // whichever of the 2 continuing directions is NOT the straight run's
    // own opposite pair; if both continuing directions ARE an opposite
    // pair (a straight run, not a seam corner), this isn't actually the
    // shape JunctionWithCorner represents — bail out rather than guess.
    bool isOppositePair = (continuing[0] + 2) % 4 == continuing[1];
    if (isOppositePair) return false;

    // The two remaining, non-continuing directions bound the "outward"
    // corner the arm should point into. yRotation follows the same N/E/S/W
    // -> 0/90/180/270 convention PrefabRuleResolver.Resolve already uses.
    yRotation = continuing[0] * 90f;
    return true;
}
```

Then change the main placement loop (lines 126-153) to check this first,
per cell, before doing anything with `PrefabRuleResolver`:

```csharp
foreach (var cell in wallCells)
{
    if (doorwayByCell.ContainsKey(cell))
    {
        // existing doorway handling, unchanged — a door always wins
        // regardless of multi-room-seam classification.
    }

    if (config.MultiRoomSeamPrefab != null &&
        TryComputeMultiRoomSeamRotation(cell, primaryRoomsByCell, FourWayDirs, out var seamRotation))
    {
        var seamInstance = PlacePiece(config.MultiRoomSeamPrefab, cell, seamRotation, config, parent);
        if (seamInstance == null) continue;
        AttachShellMarker(seamInstance, config.MultiRoomSeamPrefab, config.RuleSet, cell, RoomShellSlotRole.Wall);
        placedInstances.Add(seamInstance);
        cellByInstance[seamInstance] = cell;
        continue; // never falls through to the resolver for this cell
    }

    // existing snapshot/resolver/placement logic, unchanged, for every
    // other cell (single-room, dead-end, or non-seam cases)
    ...
}
```

Adapt this to the actual surrounding control flow in `BuildShells` — the
doorway check already exists earlier in the loop (around line 132) and must
stay first (a door always wins). Preserve `outwardDirByInstance` population
and `ShellAlignmentPass.Align` at the end exactly as today for every
instance placed, seam or not.

**Verify**: `unicli exec Compile --json` → `"errorCount": 0`.

### Step 4: Wire `MultiRoomSeamPrefab` into the window

In `LevelGenerationWindow.cs`, add the field, its `OnGUI` control, and its
`SavePendingState`/`LoadPendingState` round-trip, following the exact
`_doorPrefab` pattern cited in "Current state" above. Add it to the "Room
Shell" section of `OnGUI`, right after the existing `_doorPrefab` field.
Pass it through in `BakeLevel()`'s `ShellAssemblyConfig` construction
(currently `new ShellAssemblyConfig { RuleSet = _ruleSet, DoorPrefab =
_doorPrefab, CellSize = _cellSize }`).

**Verify**: `unicli exec Compile --json` → `"errorCount": 0`.

### Step 5: Write the regression test

Create `Assets/Game/Tests/RoomShellAssemblerMultiRoomSeamTests.cs`, modeled
directly on `Assets/Game/Tests/RoomShellAssemblerCornerTagTests.cs` (same
namespace `Game.Tests`, same "load the real `WallPrefabRuleSet` and bake
into a temp parent" structure — read that file first for the exact pattern
to copy, especially its `try`/`finally` `Object.DestroyImmediate(tempParent)`
cleanup). Use seed `1959606433` with these exact parameters (the seed this
plan was verified against):

```csharp
var parameters = new GenerationParameters
{
    Seed = 1959606433, RoomCount = 8, PuzzleRoomCount = 2, OpenSpaceCount = 1,
    MinEntryExitDistance = 3, LoopCount = 2, CorridorCount = 2,
    CorridorMinLength = 4, CorridorMaxLength = 8, IrregularRoomCount = 2,
    MinLeafSize = 3, SpareDoorCount = 0, IsolatedPocketCount = 0,
};
```

Load `Assets/Game/World/Prefabs/Environment/JunctionWithCorner.prefab` as
the `MultiRoomSeamPrefab` in the test's `ShellAssemblyConfig` (alongside the
real `WallPrefabRuleSet.asset`, same as the exemplar test does).

Assert, for every wall cell in the result whose `primaryRoomsByCell`-
equivalent membership (recompute it independently inside the test the same
way `RoomShellAssemblerCornerTagTests` recomputes `cellOwner` — do not call
any private `RoomShellAssembler` method reflectively) has 2+ distinct
rooms/margins:

- The placed instance's `PrefabBrushObject.SourcePrefab.name` equals
  `"JunctionWithCorner"` — never `"WallTjunctionPrefab"`,
  `"WallCornerPrefab_Grid"`, or `"WallStraightPrefab"`.

And, as a regression guard against Step 3 over-triggering, assert that a
sample of cells known from this session to be genuinely single-room (at
minimum, reconstruct and check cell `(24, 6)` and `(30, 6)` for this exact
seed/params — both had `ownRooms.Count == 1` when surveyed live this
session) are **not** placed as `"JunctionWithCorner"`.

**Verify**:
```bash
unicli exec AssetDatabase.Import --path "Assets/Game/Tests/RoomShellAssemblerMultiRoomSeamTests.cs" --json
unicli exec Compile --json
unicli exec TestRunner.RunEditMode '{"testNames":["Game.Tests.RoomShellAssemblerMultiRoomSeamTests"]}' --json
```
→ compile clean, all new tests pass.

### Step 6: Full regression pass

**Verify**: `unicli exec TestRunner.RunEditMode --json` → every existing
test still passes (in particular `RoomShellAssemblerCornerTagTests` and
`BspLayoutGeneratorCorridorTests`, which exercise overlapping code paths).

## Test plan

- `Assets/Game/Tests/RoomShellAssemblerMultiRoomSeamTests.cs` (new, Step 5):
  - Happy path: seed `1959606433`'s 8 known multi-room-seam cells resolve
    to `JunctionWithCorner`.
  - Regression guard: 2 known single-room cells (`24,6` / `30,6`) do not.
  - Model structurally after `Assets/Game/Tests/RoomShellAssemblerCornerTagTests.cs`.
- Verification: `unicli exec TestRunner.RunEditMode --json` → all pass,
  including the new test file.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `unicli exec Compile --json` → `"errorCount": 0`
- [ ] `unicli exec TestRunner.RunEditMode --json` → all pass, including the
      new `RoomShellAssemblerMultiRoomSeamTests`
- [ ] `git status` shows only the 4 in-scope files as modified/created
- [ ] `Assets/Game/PrefabBrush/WallPrefabRuleSet.asset` is unchanged
      (`git diff --stat -- Assets/Game/PrefabBrush/WallPrefabRuleSet.asset`
      shows no output)
- [ ] `plans/README.md` status row for `001` updated to `DONE`

## STOP conditions

Stop and report back (do not improvise) if:

- Step 1's precondition fails (`JunctionWithCorner.prefab`'s children are
  still world-absolute, not cell-relative).
- The code at the cited line numbers in "Current state" doesn't match what
  you find (the codebase has drifted since this plan was written — see the
  drift check at the top of this file).
- `continuing.Count != 2` or `isOppositePair` trips for a cell you believe
  should be a real multi-room seam during your own testing — this means the
  rotation-matching assumption in Step 3 doesn't hold for that shape, and
  guessing a rotation would place visibly wrong geometry silently.
- A test written in Step 5 fails twice after a reasonable fix attempt.
- `ShellAssemblyConfig.cs` doesn't exist at the guessed path or has a
  substantially different shape than described — locate it via `grep -rn
  "class ShellAssemblyConfig"` before assuming the guess was wrong in a way
  that changes the plan.

## Maintenance notes

- The 2 single-room notch-peninsula cells (e.g. `24,6`/`30,6` for this
  seed) are still unhandled after this plan — they're a different root
  cause (`BspLayoutGenerator.TryCarveNotch` leaving single-cell peninsulas)
  and were deliberately left out of scope. Worth a follow-up plan once
  someone characterizes exactly what shape those cells need.
- If a future change to `BspLayoutGenerator`'s door-widening/notch-carving
  logic changes how often multi-room-seam cells occur, this plan's fix
  still applies correctly — it reacts to whatever `primaryRoomsByCell`
  reports, it doesn't depend on any particular frequency.
- A reviewer should scrutinize: that `PlacePiece`'s existing rotation
  convention (composed with the prefab's own baked-in rotation) is honored
  correctly for `JunctionWithCorner` the same way it already is for every
  other piece — Step 3's `yRotation` computation should be sanity-checked
  against a live Scene-view screenshot of seed `1959606433` post-fix, not
  just the automated test, since the test only checks *which prefab* was
  placed, not that its *rotation* looks visually correct.
- This plan is a prerequisite for `plans/002-property-based-generation-invariant-sweep.md`.
