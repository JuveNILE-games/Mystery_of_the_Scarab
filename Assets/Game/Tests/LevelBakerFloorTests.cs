using System.Linq;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    // Regression coverage for floor baking being a deliberately separate step
    // from wall/Room Shell assembly (Michael, 2026-09-04): re-baking the floor
    // must never touch wall pieces, and re-baking walls must never touch floor
    // pieces, so a designer can hand-clean-up walls after Bake without a later
    // Bake Floor (or vice versa, a later Bake) wiping that work.
    public class LevelBakerFloorTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string FloorMaterialPath = "Assets/Game/Materials/Room/Floor.mat";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        // Every test gets its OWN seed — no two [Test] methods share one, full
        // stop. Originally only the wall-mutating tests got dedicated seeds,
        // but sharing one TestSeed across the rest caused real cross-test
        // contamination TWICE when several tests ran back-to-back in one
        // batch without a domain reload between them (the [SetUp]/[TearDown]'s
        // "reuse the current untitled scene" optimization doesn't fully reset
        // scene content between tests, only the scene file path + generated
        // asset folder): first a wall deleted by the merge test was still
        // missing when the door-separation test ran next against the same
        // seed's reused Level_*_Root; then, after floor meshes started being
        // persisted as real .asset files, a stale Room_*_Floor GameObject
        // left behind by an earlier test (never destroyed since the scene is
        // reused, not recreated) kept referencing a mesh asset that same
        // test's OWN teardown had just deleted from disk, throwing a
        // NullReferenceException in whichever later test happened to run
        // next. Confirmed live, 2026-09-04 and 2026-09-05 — not worth
        // special-casing per-symptom a third time.
        private const int CoversFootprintSeed = 552013;
        private const int NeverPlacesMarginFloorSeed = 552016;
        private const int IdempotentSeed = 552017;
        private const int NeverTouchesWallsSeed = 552018;
        private const int NeverTouchesFloorSeed = 552019;
        private const int UntouchedDoorwaySeed = 552020;
        private const int MergeTestSeed = 552014;
        private const int NoWallsYetTestSeed = 552015;

        private static readonly int[] AllTestSeeds =
        {
            CoversFootprintSeed, NeverPlacesMarginFloorSeed, IdempotentSeed, NeverTouchesWallsSeed,
            NeverTouchesFloorSeed, UntouchedDoorwaySeed, MergeTestSeed, NoWallsYetTestSeed
        };

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerFloorTest_{System.Guid.NewGuid():N}.unity";

            _reusedOriginalScene = string.IsNullOrEmpty(_originalActiveScene.path) &&
                                    !_originalActiveScene.isDirty;

            _tempScene = _reusedOriginalScene
                ? _originalActiveScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            EditorSceneManager.SaveScene(_tempScene, _tempScenePath);
            EditorSceneManager.SetActiveScene(_tempScene);
        }

        [TearDown]
        public void DestroyTempScene()
        {
            if (!_reusedOriginalScene)
            {
                EditorSceneManager.SetActiveScene(_originalActiveScene);
                EditorSceneManager.CloseScene(_tempScene, true);
            }

            AssetDatabase.DeleteAsset(_tempScenePath);
            foreach (var seed in AllTestSeeds)
                AssetDatabase.DeleteAsset($"Assets/Game/Systems/LevelSystem/Generated/Level_{seed}");
        }

        [Test]
        public void BakeFloors_CoversEveryFootprintCell()
        {
            // Not an exact-count match: MergeOpenSpaces can leave a connecting
            // gap cell between two merged rectangles that is real, walkable
            // floor but was never added to FootprintCells (confirmed live,
            // 2026-09-04 — the old Layout-blind floor placement silently
            // missed this gap entirely; the scene-geometry flood-fill
            // correctly fills it since there's genuinely no wall there). This
            // test only asserts the floor never falls short of what the
            // Layout itself promises, not that it stops exactly there.
            //
            // Floor is one generated mesh per room (RoomFloorMeshBuilder), not
            // one instance per cell, so coverage is measured by cell count
            // (vertexCount / 24, per RoomFloorMeshBuilder's 24-verts-per-cell
            // box — 6 faces x 4 verts, unshared layout) summed across every
            // floor mesh, not by marker count.
            var (layout, shellConfig, levelRoot) = BakeWallsOnly(CoversFootprintSeed);

            LevelBaker.BakeFloors(layout, shellConfig);

            // Margin excluded: structural cell-ownership filler, not walkable
            // gameplay space (Michael, 2026-09-04, live on Level_1399816449 —
            // "margins have a floor too, which aren't rooms").
            int expectedCells = 0;
            foreach (var room in layout.Rooms)
                if (room.Role != RoomRole.Margin)
                    expectedCells += room.FootprintCells.Count;

            int cellCount = 0;
            foreach (var marker in levelRoot.GetComponentsInChildren<PrefabBrushObject>(true))
            {
                if (marker.PlacementGroup != RoomShellAssembler.FloorPlacementGroup) continue;
                var mesh = marker.GetComponent<MeshFilter>().sharedMesh;
                cellCount += mesh.vertexCount / 24;
            }

            Assert.GreaterOrEqual(cellCount, expectedCells,
                "Expected at least one floor quad per non-margin room-owned cell across the whole Layout.");
        }

        [Test]
        public void BakeFloors_NeverPlacesFloorUnderAMarginRoom()
        {
            var (layout, shellConfig, levelRoot) = BakeWallsOnly(NeverPlacesMarginFloorSeed);

            int marginCount = 0;
            foreach (var room in layout.Rooms)
                if (room.Role == RoomRole.Margin) marginCount++;
            Assert.Greater(marginCount, 0, "Repro parameters should produce at least one margin room.");

            LevelBaker.BakeFloors(layout, shellConfig);

            foreach (var room in layout.Rooms)
            {
                if (room.Role != RoomRole.Margin) continue;

                var marginGo = levelRoot.transform.Find(room.RoomId);
                Assert.NotNull(marginGo, $"{room.RoomId}'s GameObject should still exist.");

                foreach (var marker in marginGo.GetComponentsInChildren<PrefabBrushObject>(true))
                    Assert.AreNotEqual(RoomShellAssembler.FloorPlacementGroup, marker.PlacementGroup,
                        $"{room.RoomId} is structural filler, not a gameplay room, and must not get floor tiles.");
            }
        }

        [Test]
        public void BakeFloors_IsIdempotent_RerunningDoesNotDuplicate()
        {
            var (layout, shellConfig, levelRoot) = BakeWallsOnly(IdempotentSeed);

            LevelBaker.BakeFloors(layout, shellConfig);
            int firstCount = CountByGroup(levelRoot, RoomShellAssembler.FloorPlacementGroup);

            LevelBaker.BakeFloors(layout, shellConfig);
            int secondCount = CountByGroup(levelRoot, RoomShellAssembler.FloorPlacementGroup);

            Assert.AreEqual(firstCount, secondCount, "Re-running BakeFloors should overwrite in place, never duplicate.");
        }

        [Test]
        public void BakeFloors_NeverTouchesWallPieces()
        {
            var (layout, shellConfig, levelRoot) = BakeWallsOnly(NeverTouchesWallsSeed);
            int wallCountBefore = CountByGroup(levelRoot, RoomShellAssembler.WallPlacementGroup);

            LevelBaker.BakeFloors(layout, shellConfig);
            LevelBaker.BakeFloors(layout, shellConfig); // twice, to also catch cleanup-on-rebake regressions

            int wallCountAfter = CountByGroup(levelRoot, RoomShellAssembler.WallPlacementGroup);
            Assert.AreEqual(wallCountBefore, wallCountAfter,
                "Baking (or re-baking) the floor must never add/remove wall or door pieces.");
        }

        [Test]
        public void Bake_NeverTouchesFloorPieces()
        {
            var (layout, shellConfig, levelRoot) = BakeWallsOnly(NeverTouchesFloorSeed);
            LevelBaker.BakeFloors(layout, shellConfig);
            int floorCountBefore = CountByGroup(levelRoot, RoomShellAssembler.FloorPlacementGroup);

            LevelBaker.Bake(layout, shellConfig, "FloorTestRerunWalls");

            int floorCountAfter = CountByGroup(levelRoot, RoomShellAssembler.FloorPlacementGroup);
            Assert.AreEqual(floorCountBefore, floorCountAfter,
                "Re-baking walls must never add/remove floor pieces.");
        }

        [Test]
        public void BakeFloors_CalledBeforeBake_LogsErrorAndReturnsInsteadOfHanging()
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            var parameters = new GenerationParameters
            {
                Seed = NoWallsYetTestSeed,
                RoomCount = 4,
                PuzzleRoomCount = 1,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 1,
                LoopCount = 1,
                CorridorCount = 1,
                CorridorMinLength = 4,
                CorridorMaxLength = 8,
                IrregularRoomCount = 1,
            };
            var layout = BspLayoutGenerator.Generate(parameters);
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, FloorMaterial = floorMaterial, CellSize = 1f };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "Bake Floor found no wall geometry"));
            LevelBaker.BakeFloors(layout, shellConfig);

            var levelRoot = GameObject.Find($"Level_{layout.Seed}_Root");
            Assert.IsTrue(levelRoot == null || levelRoot.GetComponentsInChildren<PrefabBrushObject>(true)
                .All(m => m.PlacementGroup != RoomShellAssembler.FloorPlacementGroup),
                "No floor should have been placed when Bake never ran first.");
        }

        [Test]
        public void BakeFloors_UntouchedDoorway_DoesNotMergeConnectedRooms()
        {
            // Integration-level counterpart to
            // RoomFloorFloodFillTests.TwoRoomsSeparatedByADoor_DoNotMerge —
            // this one exercises the real scene path (ReadSceneWallGeometry
            // against actual Door.prefab instances), not hand-authored sets.
            var (layout, shellConfig, levelRoot) = BakeWallsOnly(UntouchedDoorwaySeed);
            Assert.Greater(layout.Connections.Count, 0, "Repro parameters should produce at least one connection.");

            var (wallCells, doorCells) = RoomShellAssembler.ReadSceneWallGeometry(levelRoot, shellConfig.CellSize);
            Assert.Greater(doorCells.Count, 0, "Expected at least one real Door instance to be classified as a door cell.");

            var cellRoomIds = RoomFloorFloodFill.ComputeFloorCellsByRoom(layout, wallCells, doorCells);

            foreach (var connection in layout.Connections)
            {
                var fromRoom = layout.FindRoom(connection.FromRoomId);
                var toRoom = layout.FindRoom(connection.ToRoomId);
                if (fromRoom.FootprintCells.Count == 0 || toRoom.FootprintCells.Count == 0) continue;

                var fromResult = cellRoomIds[fromRoom.FootprintCells[0]];
                var toResult = cellRoomIds[toRoom.FootprintCells[0]];
                Assert.AreNotEqual(fromResult, toResult,
                    $"{connection.FromRoomId} and {connection.ToRoomId} are joined by an ordinary doorway and must not merge.");
            }
        }

        [Test]
        public void BakeFloors_HandDeletedWall_MergesAdjacentRooms()
        {
            var (layout, shellConfig, levelRoot) = BakeWallsOnly(MergeTestSeed);

            var cellOwner = new System.Collections.Generic.Dictionary<Vector2Int, string>();
            foreach (var room in layout.Rooms)
            foreach (var cell in room.FootprintCells)
                cellOwner[cell] = room.RoomId;

            var (wallCells, _) = RoomShellAssembler.ReadSceneWallGeometry(levelRoot, shellConfig.CellSize);
            Vector2Int? gapCell = null;
            string roomA = null, roomB = null;
            Vector2Int[] dirs = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };

            foreach (var wallCell in wallCells)
            {
                string neighborA = null, neighborB = null;
                foreach (var dir in dirs)
                {
                    if (!cellOwner.TryGetValue(wallCell + dir, out var owner)) continue;
                    if (neighborA == null) neighborA = owner;
                    else if (owner != neighborA) neighborB = owner;
                }

                if (neighborA == null || neighborB == null) continue;
                gapCell = wallCell;
                roomA = neighborA;
                roomB = neighborB;
                break;
            }

            Assert.IsTrue(gapCell.HasValue, "Repro parameters should produce at least one direct room-to-room shared wall cell.");

            // XZ-only distance, generous fixed margin — not tied to
            // shellConfig.CellSize: a wall piece can be shifted off the
            // nominal cell center by up to half a cell (ShellAlignmentPass),
            // and a Y-inclusive 3D distance compared against CellSize itself
            // got too tight to reliably match once CellSize shrank from 2 to
            // 1 (confirmed live, 2026-09-05 — this is a test-search-radius
            // bug, not a production issue).
            var cellCenterXz = new Vector2(gapCell.Value.x * shellConfig.CellSize, gapCell.Value.y * shellConfig.CellSize);
            int destroyed = 0;
            foreach (var marker in levelRoot.GetComponentsInChildren<PrefabBrushObject>(true))
            {
                if (marker.PlacementGroup != RoomShellAssembler.WallPlacementGroup) continue;
                if (marker.GetComponent<DoorwayMarker>() != null) continue;
                var pos = marker.transform.position;
                if (Vector2.Distance(new Vector2(pos.x, pos.z), cellCenterXz) > shellConfig.CellSize + 0.6f) continue;
                Object.DestroyImmediate(marker.gameObject);
                destroyed++;
            }

            Assert.Greater(destroyed, 0, $"Expected to find and delete a wall piece at {gapCell.Value}.");

            LevelBaker.BakeFloors(layout, shellConfig);

            // Re-derive directly rather than inspecting GameObject parentage:
            // "first-claimed-wins" means every cell in the merged region
            // (both rooms' original cells) ends up attributed to whichever
            // room's flood got there first, so the "losing" room's own
            // GameObject folder can end up with zero floor children of its
            // own — that's correct merge behavior, not something to assert
            // against.
            var (wallCellsAfter, doorCellsAfter) = RoomShellAssembler.ReadSceneWallGeometry(levelRoot, shellConfig.CellSize);
            var cellRoomIdsAfter = RoomFloorFloodFill.ComputeFloorCellsByRoom(layout, wallCellsAfter, doorCellsAfter);

            var roomACell = cellOwner.First(kv => kv.Value == roomA).Key;
            var roomBCell = cellOwner.First(kv => kv.Value == roomB).Key;
            Assert.AreEqual(cellRoomIdsAfter[roomACell], cellRoomIdsAfter[roomBCell],
                $"{roomA} and {roomB} share a hand-deleted wall and should now merge into one floor region.");
        }

        private static int CountByGroup(GameObject levelRoot, string placementGroup)
        {
            int count = 0;
            foreach (var marker in levelRoot.GetComponentsInChildren<PrefabBrushObject>(true))
                if (marker.PlacementGroup == placementGroup)
                    count++;
            return count;
        }

        // No default seed — every call site must pass its own dedicated seed
        // explicitly (see AllTestSeeds comment above for why).
        private static (Layout layout, ShellAssemblyConfig shellConfig, GameObject levelRoot) BakeWallsOnly(int seed)
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");

            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            Assert.NotNull(floorMaterial, $"Could not load {FloorMaterialPath} — is the path still correct?");

            // A real Door prefab, not null: without one, every connection's
            // doorway cell gets no piece placed at all (LevelBaker.Bake skips
            // it outright), which the flood-fill correctly reads as a genuine
            // open gap and floods straight through — merging rooms an actual
            // bake's doorway would have kept separate. Confirmed live,
            // 2026-09-04.
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");

            var parameters = new GenerationParameters
            {
                Seed = seed,
                RoomCount = 4,
                PuzzleRoomCount = 1,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 1,
                LoopCount = 1,
                CorridorCount = 1,
                CorridorMinLength = 4,
                CorridorMaxLength = 8,
                IrregularRoomCount = 1,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var shellConfig = new ShellAssemblyConfig
                { RuleSet = ruleSet, DoorPrefab = doorPrefab, FloorMaterial = floorMaterial, CellSize = 1f };
            LevelBaker.Bake(layout, shellConfig, "FloorTestWalls");
            var levelRoot = GameObject.Find($"Level_{layout.Seed}_Root");
            return (layout, shellConfig, levelRoot);
        }
    }
}
