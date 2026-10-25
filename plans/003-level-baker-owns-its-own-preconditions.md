# Plan 003: LevelBaker's bake steps own their own stage/precondition setup, instead of leaving it to caller-side convention

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat dc443d1..HEAD -- Assets/Game/Systems/LevelGeneration/Editor/LevelBaker.cs Assets/Game/Systems/LevelGeneration/Editor/LevelGenerationWindow.cs`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: LOW
- **Depends on**: none (independent of `plans/001` and `plans/002`, safe to
  execute in any order relative to them — touches different files)
- **Category**: tech-debt
- **Planned at**: commit `dc443d1`, 2026-09-21

**Visual guide**: `C:\Users\Micha\AppData\Local\Temp\plan-003-visual-guide.html`
(sequence diagram of today's caller-side setup, before/after interface shape)

## Why this matters

`LevelBaker` exposes 8 public static entry points (`Bake`, `BakeFloors`,
`LoadOrCreateDefaultCuration`, `LoadOrCreateRoomShellEditHistory`,
`EnsureDefaultPocketCuration`, `BakeConnections`,
`EnsureLevelPrefabStageOpen`, `EnsureInstanceInScene`). Its one caller,
`LevelGenerationWindow`, has to know — and currently documents only in
prose comments on its own button handlers — that `EnsureLevelPrefabStageOpen`
must be called *before* each of `Bake`/`BakeFloors`/`BakeConnections`, every
time, or the step silently operates against the wrong scene/stage. It also
implements two *different* ad hoc checks for "has `Bake` already run for
this seed" — `BakeFloors` infers it after the fact from an empty wall-cell
set, `BakeConnections` checks root existence directly — so the same
precondition is expressed twice, differently, and a future third caller
(a batch-bake script, a different tool window) would have to rediscover
both the stage-opening convention and reinvent one of the two precondition
checks by reading `LevelGenerationWindow.cs`'s comments, not by reading
`LevelBaker`'s own public surface.

This plan moves both responsibilities inside `LevelBaker` itself: each bake
step opens its own stage and checks its own precondition, so a caller only
has to call the one method for the step it wants, in the order the domain
already requires (documented, not silently enforced beyond what's below —
see "Out of scope").

## Current state

- `Assets/Game/Systems/LevelGeneration/Editor/LevelBaker.cs` — full file
  already read for this plan. Relevant excerpts:

  `EnsureLevelPrefabStageOpen` (lines 661-680), called by the window before
  each bake action today, never internally by `Bake`/`BakeFloors`/
  `BakeConnections` themselves:

  ```csharp
  // lines 661-680
  public static string EnsureLevelPrefabStageOpen(int seed)
  {
      string folder = $"Assets/Game/Systems/LevelSystem/Generated/Level_{seed}";
      EnsureFolder(folder);
      string prefabPath = $"{folder}/Level_{seed}_Root.prefab";

      var currentStage = PrefabStageUtility.GetCurrentPrefabStage();
      if (currentStage != null && currentStage.assetPath == prefabPath)
          return prefabPath;

      if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
      {
          var placeholder = new GameObject($"Level_{seed}_Root");
          PrefabUtility.SaveAsPrefabAsset(placeholder, prefabPath);
          Object.DestroyImmediate(placeholder);
      }

      PrefabStageUtility.OpenPrefab(prefabPath);
      return prefabPath;
  }
  ```

  `BakeFloors`'s current, after-the-fact precondition check (lines 156-176):

  ```csharp
  // lines 156-176 (abridged)
  public static void BakeFloors(Layout layout, ShellAssemblyConfig shellConfig)
  {
      var levelRoot = FindOrCreateRoot($"Level_{layout.Seed}_Root");
      var roomGoById = new Dictionary<string, GameObject>();
      foreach (var roomNode in layout.Rooms)
          roomGoById[roomNode.RoomId] = FindOrCreateChild(levelRoot.transform, roomNode.RoomId);

      var (wallCells, doorCells) = RoomShellAssembler.ReadSceneWallGeometry(levelRoot, shellConfig.CellSize);

      if (wallCells.Count == 0 && layout.Rooms.Count > 0)
      {
          Debug.LogError("[LevelGeneration] Bake Floor found no wall geometry under " +
                          $"'{levelRoot.name}' — run Bake first for this seed.");
          return;
      }
      ...
  ```

  `BakeConnections`'s current, direct root-existence check (lines 374-392):

  ```csharp
  // lines 374-392 (abridged)
  public static void BakeConnections(Layout layout, LevelConnectionCuration curation,
      NonEuclideanTransitionDefaults transitionDefaults)
  {
      if (curation.Seed != layout.Seed) { ... return; }

      var rootName = $"Level_{layout.Seed}_Root";
      var scene = GetWorkingScene();
      if (!scene.GetRootGameObjects().Any(r => r.name == rootName))
      {
          Debug.LogError($"[LevelGeneration] '{rootName}' does not exist in the active scene — run Bake first.");
          return;
      }

      var root = FindOrCreateRoot(rootName); // safe now — existence already confirmed above
      ...
  ```

  `GetWorkingScene` (lines 648-652) — unchanged by this plan, used by the
  new shared helper below:

  ```csharp
  private static Scene GetWorkingScene()
  {
      var stage = PrefabStageUtility.GetCurrentPrefabStage();
      return stage != null ? stage.scene : EditorSceneManager.GetActiveScene();
  }
  ```

- `Assets/Game/Systems/LevelGeneration/Editor/LevelGenerationWindow.cs` —
  `BakeLevel()`, `BakeFloor()`, `BakeConnections()` (lines 144-215, already
  read this session). Each currently opens with its own
  `LevelBaker.EnsureLevelPrefabStageOpen(...)` call before doing anything
  else, e.g.:

  ```csharp
  // BakeFloor(), lines 184-195
  private void BakeFloor()
  {
      LevelBaker.EnsureLevelPrefabStageOpen(_previewLayout.Seed);

      var shellConfig = new ShellAssemblyConfig { ... };
      LevelBaker.BakeFloors(_previewLayout, shellConfig);
      Debug.Log($"[LevelGeneration] Baked floor for 'Level_{_previewLayout.Seed}'.");
  }
  ```

  `BakeConnections()` (lines 197-215) has the same
  `EnsureLevelPrefabStageOpen` call at its top, before curation loading.

- Repo convention: `System.Linq` is already imported in `LevelBaker.cs`
  (used by `BakeConnections`'s `.ToDictionary`/`.Any` — see the `using`
  block at the top of the file), so `.FirstOrDefault` is available without
  a new import.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Import a changed file | `unicli exec AssetDatabase.Import --path "<path>" --json` | `"success": true` |
| Compile | `unicli exec Compile --json` | `"errorCount": 0` |
| Run the new/updated tests | `unicli exec TestRunner.RunEditMode '{"testNames":["Game.Tests.LevelBakerPreconditionTests"]}' --json` | all pass |
| Full regression (existing bake tests exercise this code heavily) | `unicli exec TestRunner.RunEditMode --json` | all existing tests still pass |

## Suggested executor toolkit

- `unicli:unity-development` skill for all Editor automation.

## Scope

**In scope** (the only files you should modify):
- `Assets/Game/Systems/LevelGeneration/Editor/LevelBaker.cs`
- `Assets/Game/Systems/LevelGeneration/Editor/LevelGenerationWindow.cs`
- `Assets/Game/Tests/LevelBakerPreconditionTests.cs` (create)

**Out of scope** (do NOT touch, even though they look related):
- The intentional 3-way split itself (`Bake`/`BakeFloors`/`BakeConnections`
  staying 3 separate, independently-callable methods). Comments in
  `LevelGenerationWindow.cs` (lines 117-124, 131-136) record this as
  Michael's deliberate design ("walls get baked and hand-cleaned up first,
  floor gets baked afterward on top of whatever the walls ended up looking
  like... Re-running this never touches wall pieces, and re-running Bake
  never touches floor pieces"). This plan does not collapse the 3 steps
  into one call, does not add a stateful session/builder type, and does
  not change what each step is responsible for — it only moves *where* the
  stage-opening and precondition-checking code lives.
- `LoadOrCreateDefaultCuration`, `LoadOrCreateRoomShellEditHistory`,
  `EnsureDefaultPocketCuration`, `EnsureInstanceInScene` — unrelated to the
  stage-opening/precondition duplication this plan fixes.
- Any change to error message wording beyond what Step 2 specifies exactly
  — do not "improve" unrelated log messages while in this file.

## Git workflow

- Stay on the existing branch `prototype/room-generator` — see
  `SCARAB-MAP-01`'s note that all level-generation work happens there, not
  on a new branch.
- Commit message style: `[TASK]` prefix (internal refactor, not a bug fix
  or new feature), body explains why. End with
  `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Do NOT push unless the operator instructed it.

## Steps

### Step 1: Fold `EnsureLevelPrefabStageOpen` into each bake step

In `LevelBaker.cs`, add one line at the top of `Bake`, `BakeFloors`, and
`BakeConnections`:

```csharp
public static LevelDefinition Bake(Layout layout, ShellAssemblyConfig shellConfig, string levelDisplayName)
{
    EnsureLevelPrefabStageOpen(layout.Seed);
    string folder = $"Assets/Game/Systems/LevelSystem/Generated/Level_{layout.Seed}";
    ... // rest unchanged
```

```csharp
public static void BakeFloors(Layout layout, ShellAssemblyConfig shellConfig)
{
    EnsureLevelPrefabStageOpen(layout.Seed);
    ... // rest changes in Step 2
```

```csharp
public static void BakeConnections(Layout layout, LevelConnectionCuration curation,
    NonEuclideanTransitionDefaults transitionDefaults)
{
    if (curation.Seed != layout.Seed) { ... return; } // unchanged, stays first
    EnsureLevelPrefabStageOpen(layout.Seed);
    ... // rest changes in Step 2
```

Keep `EnsureLevelPrefabStageOpen` itself public and unchanged — it stays
useful standalone (e.g. `EnsureInstanceInScene`'s caller in the window
still needs a stage open before it runs, and nothing in this plan removes
that need).

**Verify**: `unicli exec Compile --json` → `"errorCount": 0`.

### Step 2: Add the shared precondition helper and use it in both `BakeFloors` and `BakeConnections`

Add this private helper near `GetWorkingScene` (around line 648):

```csharp
// Shared precondition for BakeFloors/BakeConnections (SCARAB-013 follow-up,
// 2026-09-21): both require Bake() to have already run at least once for
// this seed. Previously expressed two different ways — BakeFloors inferred
// it after the fact from an empty wall-cell set, BakeConnections checked
// root existence directly — centralized so both give the same precise
// error and neither risks silently proceeding against an auto-created,
// empty root (FindOrCreateRoot would otherwise create one).
private static bool RequireLevelRootExists(int seed, string callerLabel, out GameObject root)
{
    var rootName = $"Level_{seed}_Root";
    var scene = GetWorkingScene();
    root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == rootName);
    if (root != null) return true;

    Debug.LogError($"[LevelGeneration] {callerLabel}: '{rootName}' does not exist " +
                    "in the active scene — run Bake first.");
    return false;
}
```

Update `BakeFloors` (replace its `FindOrCreateRoot` call and remove the
later `wallCells.Count == 0` check, since the root-existence check now runs
first and is strictly more precise):

```csharp
public static void BakeFloors(Layout layout, ShellAssemblyConfig shellConfig)
{
    EnsureLevelPrefabStageOpen(layout.Seed);
    if (!RequireLevelRootExists(layout.Seed, "Bake Floor", out var levelRoot)) return;

    var roomGoById = new Dictionary<string, GameObject>();
    foreach (var roomNode in layout.Rooms)
        roomGoById[roomNode.RoomId] = FindOrCreateChild(levelRoot.transform, roomNode.RoomId);

    var (wallCells, doorCells) = RoomShellAssembler.ReadSceneWallGeometry(levelRoot, shellConfig.CellSize);
    // the old "wallCells.Count == 0 && layout.Rooms.Count > 0" early-return block is removed —
    // RequireLevelRootExists above already catches the "Bake never ran" case earlier and more
    // precisely. If wallCells is legitimately empty for some OTHER reason with a root present,
    // RoomFloorFloodFill's own downstream sanity cap still applies unchanged.
    ...
```

Update `BakeConnections` to use the same helper in place of its inline
check:

```csharp
public static void BakeConnections(Layout layout, LevelConnectionCuration curation,
    NonEuclideanTransitionDefaults transitionDefaults)
{
    if (curation.Seed != layout.Seed)
    {
        Debug.LogError($"[LevelGeneration] Curation asset targets seed {curation.Seed}, " +
                        $"but the current layout is seed {layout.Seed} — aborting.");
        return;
    }

    EnsureLevelPrefabStageOpen(layout.Seed);
    if (!RequireLevelRootExists(layout.Seed, "Bake Connections", out var root)) return;

    var scene = GetWorkingScene();
    foreach (var existingTrigger in root.GetComponentsInChildren<DoorwayTeleportTrigger>(true))
        Undo.DestroyObjectImmediate(existingTrigger);
    ... // rest unchanged, `root` and `scene` already in scope
```

**Verify**: `unicli exec Compile --json` → `"errorCount": 0`.

### Step 3: Remove the now-redundant calls from `LevelGenerationWindow.cs`

In `BakeLevel()`, `BakeFloor()`, and `BakeConnections()`, delete the
`LevelBaker.EnsureLevelPrefabStageOpen(...)` line at the top of each — the
corresponding `LevelBaker` method now does this itself. Leave every other
line in these three methods unchanged (including `EnsureInstanceInScene`'s
call in `BakeLevel()`, which still needs the stage to already be open at
that point — it now is, via `Bake()`'s own internal call).

**Verify**: `unicli exec Compile --json` → `"errorCount": 0`.

### Step 4: Write the precondition regression test

Create `Assets/Game/Tests/LevelBakerPreconditionTests.cs`, modeled on the
temp-scene setup/teardown pattern in
`Assets/Game/Tests/LevelBakerPocketReachabilityTests.cs` (read that file
first — same `[SetUp]`/`[TearDown]` temp-scene creation, same generated-
asset cleanup in `[TearDown]` keyed off a `_bakedSeed` field). Cover:

- `BakeFloors` called for a seed that was never `Bake`d: assert
  `LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Bake Floor.*does not exist.*run Bake first.*"))`
  fires and no floor GameObjects are created.
- `BakeConnections` called for a seed that was never `Bake`d: same pattern,
  asserting the `"Bake Connections"` label appears in the error.
- `BakeFloors` called after a real `Bake()` for the same seed: no error
  logged (`LogAssert.NoUnexpectedReceived()` after the call), floor tiles
  are created as before.

**Verify**:
```bash
unicli exec AssetDatabase.Import --path "Assets/Game/Tests/LevelBakerPreconditionTests.cs" --json
unicli exec Compile --json
unicli exec TestRunner.RunEditMode '{"testNames":["Game.Tests.LevelBakerPreconditionTests"]}' --json
```
→ compile clean, all pass.

### Step 5: Full regression pass, then a live sanity bake

**Verify (automated)**: `unicli exec TestRunner.RunEditMode --json` → every
existing test still passes (in particular
`LevelBakerPocketReachabilityTests`, `LevelBakerDefaultPocketCurationTests`,
`LevelBakerRoomShellReconcilerTests`, and
`LevelBakerRoomShellReconcilerSteadyStateTests`, which all exercise
`LevelBaker.Bake`/`BakeFloors`/`BakeConnections` directly).

**Verify (manual, cannot be scripted — do this yourself in the open Unity
Editor)**: open `Tools > Level Generation > Generate Level`, click
**Bake**, then **Bake Floor**, then **Bake Connections** for any seed, and
confirm each still switches into/stays in the correct Prefab Stage and
completes without a new console error. This is the actual end-user path
this plan touches; the automated tests above exercise `LevelBaker`
directly, not the window's button click-through.

## Test plan

- `Assets/Game/Tests/LevelBakerPreconditionTests.cs` (new, Step 4): the 3
  cases listed above.
- Model structurally after `Assets/Game/Tests/LevelBakerPocketReachabilityTests.cs`.
- Verification: `unicli exec TestRunner.RunEditMode --json` → all pass,
  including the new file.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `unicli exec Compile --json` → `"errorCount": 0`
- [ ] `unicli exec TestRunner.RunEditMode --json` → all pass, including the
      new `LevelBakerPreconditionTests`
- [ ] `grep -n "EnsureLevelPrefabStageOpen" Assets/Game/Systems/LevelGeneration/Editor/LevelGenerationWindow.cs`
      returns no matches (the window no longer calls it directly)
- [ ] `git status` shows only the 3 in-scope files as modified/created
- [ ] Step 5's manual Bake/Bake Floor/Bake Connections click-through
      completed with no new console error
- [ ] `plans/README.md` status row for `003` updated to `DONE`

## STOP conditions

Stop and report back (do not improvise) if:

- The code at the cited line numbers in "Current state" doesn't match what
  you find (the codebase has drifted since this plan was written).
- Removing `EnsureLevelPrefabStageOpen` from any of the 3 window methods
  changes observed behavior in the manual Step 5 check (e.g. a different
  Prefab Stage ends up open than before) — this would mean the call order
  matters in a way this plan didn't account for (e.g. something between the
  old call site and the `LevelBaker.X(...)` call in the window depended on
  the stage already being open). Do not paper over it by re-adding the call
  to the window "just in case" — report the exact discrepancy instead.
- A test written in Step 4 fails twice after a reasonable fix attempt.

## Maintenance notes

- A future 4th caller of `LevelBaker` (a batch-bake script, CI tooling)
  gets the stage-opening and precondition behavior for free now — no need
  to rediscover `LevelGenerationWindow.cs`'s old comment-only convention.
- If a future change adds a 4th bake step with its own "must run after X"
  requirement, follow this plan's `RequireLevelRootExists` pattern rather
  than reinventing a third ad hoc check.
- A reviewer should scrutinize: that `BakeFloors`' removed
  `wallCells.Count == 0` check truly was redundant with the new
  `RequireLevelRootExists` check and not covering some *other* case (e.g. a
  root that exists but was cleared of wall geometry by something other than
  "Bake never ran") — Step 4's test list does not cover that specific edge
  case; if it turns out to matter, it's a gap to flag, not silently ignore.
