using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Tools.PrefabBrush;
using Eflatun.SceneReference;
using Game.Systems.LevelSystem.Definitions;
using Game.Systems.LevelSystem.Runtime;
using Game.Systems.PuzzleSystem.Definitions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // Layout-to-asset baking mapping (SCARAB-006): seed-derived deterministic
    // naming with overwrite-in-place re-bakes in a Generated/ subfolder;
    // sceneRootTag populated despite being dead code; RoomController._definition
    // wired via direct SerializedObject assignment, mirroring manual Inspector
    // binding. Bakes into the currently active scene (SCARAB-003's full M:N
    // multi-scene schema is not yet applied to LevelDefinition/RoomDefinition —
    // deferred until an example actually needs more than one scene).
    public static class LevelBaker
    {
        private static readonly Vector2Int[] Dirs4 =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0)
        };

        private static readonly Vector2Int[] DiagonalDirs4 =
        {
            new(1, 1), new(1, -1), new(-1, -1), new(-1, 1)
        };

        public static LevelDefinition Bake(Layout layout, ShellAssemblyConfig shellConfig, string levelDisplayName)
        {
            string folder = $"Assets/Game/Systems/LevelSystem/Generated/Level_{layout.Seed}";
            EnsureFolder(folder);

            var levelDef = LoadOrCreateAsset<LevelDefinition>($"{folder}/Level_{layout.Seed}.asset");
            levelDef.levelId = $"Level_{layout.Seed}";
            levelDef.displayName = string.IsNullOrEmpty(levelDisplayName) ? levelDef.levelId : levelDisplayName;
            levelDef.levelScene = SceneReference.FromScenePath(EditorSceneManager.GetActiveScene().path);

            var levelRoot = FindOrCreateRoot($"Level_{layout.Seed}_Root");

            // SCARAB-015: captured before the wipe below destroys everything — an object with no
            // RoomShellSlotAnchor yet (a level baked before this feature existed) is captured
            // unidentified, not skipped; Replay's legacy fallback recovers its identity once the
            // fresh regeneration exists to match it against.
            var roomShellHistory = LoadOrCreateRoomShellEditHistory(layout.Seed);
            var preWipeShellSlots = RoomShellReconciler.Capture(levelRoot, RoomShellAssembler.WallPlacementGroup);

            var roomDefsById = new Dictionary<string, RoomDefinition>();
            var roomGoById = new Dictionary<string, GameObject>();

            foreach (var roomNode in layout.Rooms)
            {
                var (roomDef, roomGo) = BakeRoomShell(folder, roomNode, levelRoot.transform);
                if (roomDef != null) roomDefsById[roomNode.RoomId] = roomDef;
                roomGoById[roomNode.RoomId] = roomGo;
            }

            AssembleAndDistributeShells(layout, shellConfig, roomGoById, levelRoot);

            var replayReport = RoomShellReconciler.Replay(levelRoot, RoomShellAssembler.WallPlacementGroup,
                preWipeShellSlots, roomShellHistory, shellConfig.CellSize);
            LogRoomShellReplayReport(replayReport);
            EditorUtility.SetDirty(roomShellHistory);

            foreach (var connection in layout.Connections)
            {
                // A Spare or Pocket door (SCARAB-014) has ToRoomId == null — no
                // destination until a curator pairs it (ADR 0001) — so it never
                // gets a RoomConnection entry here; BakeConnections is what wires
                // it up later, onto DoorwayTeleportTrigger, not RoomDefinition.
                if (connection.ToRoomId == null) continue;
                if (!roomDefsById.TryGetValue(connection.FromRoomId, out var fromDef)) continue;
                if (!roomDefsById.TryGetValue(connection.ToRoomId, out var toDef)) continue;

                AddOrUpdateConnection(fromDef, toDef, connection.DoorwayId);
                AddOrUpdateConnection(toDef, fromDef, connection.DoorwayId);
            }

            levelDef.rooms = new List<RoomDefinition>(roomDefsById.Values);
            var entryNode = layout.Rooms.Find(r => r.IsEntry);
            var exitNode = layout.Rooms.Find(r => r.IsExit);
            levelDef.entryRoom = entryNode != null ? roomDefsById[entryNode.RoomId] : null;
            levelDef.exitRoom = exitNode != null ? roomDefsById[exitNode.RoomId] : null;

            foreach (var def in roomDefsById.Values)
                EditorUtility.SetDirty(def);
            EditorUtility.SetDirty(levelDef);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return levelDef;
        }

        // A margin room (RoomRole.Margin) is structural filler, not a
        // gameplay room — see BspLayoutGenerator.Generate's graphRooms split.
        // It still needs a plain GameObject so AssembleAndDistributeShells
        // has somewhere sane to parent its wall pieces, but gets no
        // RoomDefinition asset, no RoomController, and never enters
        // levelDef.rooms/connections.
        private static (RoomDefinition, GameObject) BakeRoomShell(string folder, LayoutRoomNode roomNode, Transform levelRoot)
        {
            if (roomNode.Role == RoomRole.Margin)
            {
                var marginGo = FindOrCreateChild(levelRoot, roomNode.RoomId);
                ClearGeneratedShellChildren(marginGo.transform);
                return (null, marginGo);
            }

            bool isPuzzle = roomNode.Role == RoomRole.Puzzle;
            string assetPath = $"{folder}/{roomNode.RoomId}.asset";

            RoomDefinition roomDef = isPuzzle
                ? LoadOrCreateAsset<PuzzleRoomDefinition>(assetPath)
                : LoadOrCreateAsset<RoomDefinition>(assetPath);

            roomDef.roomId = roomNode.RoomId;
            roomDef.displayName = roomNode.RoomId;
            roomDef.connections = new List<RoomConnection>();

            if (roomDef is PuzzleRoomDefinition puzzleDef)
                puzzleDef.puzzles ??= new List<PuzzleDefinition>();

            var roomGo = FindOrCreateChild(levelRoot, roomNode.RoomId);
            roomDef.sceneRootTag = roomGo.name;

            ClearGeneratedShellChildren(roomGo.transform);

            var controllerType = isPuzzle ? typeof(PuzzleRoomController) : typeof(RoomController);
            var controller = roomGo.GetComponent(controllerType) as RoomController
                              ?? (RoomController)Undo.AddComponent(roomGo, controllerType);
            AssignDefinition(controller, roomDef);

            return (roomDef, roomGo);
        }

        // Floor is a deliberately separate bake step from Bake's wall/Room
        // Shell assembly (Michael, 2026-09-04): Bake and BakeFloors must be
        // safe to run independently, any number of times, in either order,
        // without one clobbering hand-edits made after the other last ran.
        // Requires Bake to have already run at least once for this seed (the
        // level root and per-room GameObjects must already exist) — it does
        // not create/wire RoomDefinition assets, RoomControllers, or
        // connections, only floor tiles under the existing room hierarchy.
        public static void BakeFloors(Layout layout, ShellAssemblyConfig shellConfig)
        {
            var levelRoot = FindOrCreateRoot($"Level_{layout.Seed}_Root");

            var roomGoById = new Dictionary<string, GameObject>();
            foreach (var roomNode in layout.Rooms)
                roomGoById[roomNode.RoomId] = FindOrCreateChild(levelRoot.transform, roomNode.RoomId);

            var (wallCells, doorCells) = RoomShellAssembler.ReadSceneWallGeometry(levelRoot, shellConfig.CellSize);

            // Fail loudly rather than handing the flood-fill an empty wall
            // set — without this, clicking Bake Floor before Bake (or against
            // a scene whose wall geometry was otherwise wiped out) would only
            // be caught by RoomFloorFloodFill's sanity cap, several steps
            // downstream of the actual mistake.
            if (wallCells.Count == 0 && layout.Rooms.Count > 0)
            {
                Debug.LogError("[LevelGeneration] Bake Floor found no wall geometry under " +
                                $"'{levelRoot.name}' — run Bake first for this seed.");
                return;
            }

            foreach (var roomGo in roomGoById.Values)
                ClearGeneratedShellChildren(roomGo.transform, RoomShellAssembler.FloorPlacementGroup);

            var cellRoomIds = RoomFloorFloodFill.ComputeFloorCellsByRoom(layout, wallCells, doorCells);

            var cellsByRoom = new Dictionary<string, List<Vector2Int>>();
            foreach (var (cell, roomId) in cellRoomIds)
            {
                if (!cellsByRoom.TryGetValue(roomId, out var cells))
                    cellsByRoom[roomId] = cells = new List<Vector2Int>();
                cells.Add(cell);
            }

            // One generated mesh per room (Michael, 2026-09-04), not one
            // instance per cell — sidesteps the whole "does a tile prefab's
            // mesh/import/transform scale match CellSize" class of bug
            // phase-1 hit, and is far cheaper (one draw call per room).
            //
            // Persisted as a real .asset (not left in-memory-only): a plain
            // `new Mesh()` has no asset identity, so Unity refuses "Apply to
            // Prefab" for any MeshFilter referencing one — there is nothing
            // for the prefab file to point at (confirmed live, 2026-09-05,
            // trying to sync Level_1399816449_Root.prefab). Load-or-create +
            // repopulate in place (never delete-then-recreate) so the SAME
            // asset identity survives every re-bake, matching the same
            // pattern LoadOrCreateAsset already uses for RoomDefinition/
            // LevelDefinition.
            string folder = $"Assets/Game/Systems/LevelSystem/Generated/Level_{layout.Seed}";
            EnsureFolder(folder);

            foreach (var (roomId, cells) in cellsByRoom)
            {
                if (!roomGoById.TryGetValue(roomId, out var roomGo)) continue;

                string meshPath = $"{folder}/{roomId}_FloorMesh.asset";
                var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                var mesh = RoomFloorMeshBuilder.BuildRoomFloorMesh(cells, wallCells, shellConfig.CellSize,
                    shellConfig.FloorUvMode, shellConfig.FloorThickness, existingMesh);
                mesh.name = $"{roomId}_FloorMesh"; // match the asset filename, avoids a Unity import warning

                if (existingMesh == null)
                    AssetDatabase.CreateAsset(mesh, meshPath);
                else
                    EditorUtility.SetDirty(mesh);

                var floorGo = new GameObject($"{roomId}_Floor");
                Undo.RegisterCreatedObjectUndo(floorGo, "Bake Floor");
                floorGo.transform.SetParent(roomGo.transform, false);

                floorGo.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = floorGo.AddComponent<MeshRenderer>();
                if (shellConfig.FloorMaterial != null) renderer.sharedMaterial = shellConfig.FloorMaterial;
                floorGo.AddComponent<MeshCollider>().sharedMesh = mesh;

                var marker = floorGo.AddComponent<PrefabBrushObject>();
                marker.PlacementGroup = RoomShellAssembler.FloorPlacementGroup;
                marker.PlacementTime = System.DateTime.UtcNow.ToString("o");
                marker.SurfaceNormal = Vector3.up;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        // Standalone, sync-to-declared-state step (SCARAB-012) — deliberately separate from
        // Bake/BakeFloors (Michael, 2026-09-05): curation is a human decision made by hand-
        // editing a LevelConnectionCuration asset, applied after the fact, so this must never
        // touch wall/room/floor geometry and must be safe to run against an existing bake
        // whose room layout has since drifted from hand-edits. Never creates the level root —
        // unlike Bake/BakeFloors, it's meant to upgrade a bake that must already exist.
        //
        // Reset-then-reapply, matching BakeFloors' own idempotency discipline
        // (ClearGeneratedShellChildren before rebuilding): every existing
        // DoorwayTeleportTrigger is stripped before curation.Pairings is reapplied, so
        // shrinking or editing the curation list actually converges to the declared state
        // instead of only ever accumulating stale triggers/pairings from earlier runs.
        // Same Generated/Level_{seed}/ folder convention as Bake/BakeFloors — see
        // LevelConnectionCuration's own doc comment ("one asset per generated
        // level"). Reuses the level's existing asset if one is already there
        // (e.g. from a prior session) rather than overwriting a curator's work.
        public static LevelConnectionCuration LoadOrCreateDefaultCuration(int seed)
        {
            string folder = $"Assets/Game/Systems/LevelSystem/Generated/Level_{seed}";
            EnsureFolder(folder);
            var curation = LoadOrCreateAsset<LevelConnectionCuration>($"{folder}/ConnectionCuration.asset");
            curation.Seed = seed;
            return curation;
        }

        // SCARAB-015: same load-or-create/reuse-if-present convention as
        // LoadOrCreateDefaultCuration — Bake() calls this on every bake, not just the first, so
        // an existing asset's Edits must survive untouched, never get overwritten with a fresh
        // empty instance.
        public static RoomShellEditHistory LoadOrCreateRoomShellEditHistory(int seed)
        {
            string folder = $"Assets/Game/Systems/LevelSystem/Generated/Level_{seed}";
            EnsureFolder(folder);
            var history = LoadOrCreateAsset<RoomShellEditHistory>($"{folder}/RoomShellEditHistory.asset");
            history.Seed = seed;
            return history;
        }

        // SCARAB-014 follow-up (2026-09-06, Michael): a generated Isolated Pocket
        // must be reachable out of the box, not silently unreachable until a
        // curator happens to hand-author a pairing for it. Additive only — never
        // touches a pairing the curator already made by hand (referencing either
        // side of it), never reuses an ordinary main-graph door across two
        // default pairings. Everything else (a Pocket's Exit, Spare Doors,
        // Felt-vs-Hidden choices) is still entirely up to the curator; this only
        // guarantees the minimum needed for the Pocket to not ship broken.
        public static void EnsureDefaultPocketCuration(Layout layout, LevelConnectionCuration curation)
        {
            if (layout.Pockets.Count == 0) return;

            var referencedDoorwayIds = new HashSet<string>();
            foreach (var pairing in curation.Pairings)
            {
                referencedDoorwayIds.Add(pairing.DoorwayIdA);
                referencedDoorwayIds.Add(pairing.DoorwayIdB);
            }

            var availableOrdinaryDoors = new List<string>();
            foreach (var c in layout.Connections)
                if (!c.IsSpareDoor && c.ToRoomId != null && !referencedDoorwayIds.Contains(c.DoorwayId))
                    availableOrdinaryDoors.Add(c.DoorwayId);

            int nextOrdinaryIndex = 0;
            foreach (var pocket in layout.Pockets)
            {
                if (pocket.EntranceDoorwayId == null) continue; // generation couldn't place a door for it at all

                bool alreadyCovered = referencedDoorwayIds.Contains(pocket.EntranceDoorwayId) ||
                                       (pocket.ExitDoorwayId != null &&
                                        referencedDoorwayIds.Contains(pocket.ExitDoorwayId));
                if (alreadyCovered) continue;

                if (nextOrdinaryIndex >= availableOrdinaryDoors.Count)
                {
                    Debug.LogWarning($"[LevelGeneration] No unused ordinary door left to default-curate Pocket " +
                                      $"{pocket.PocketId} — add a pairing for it by hand.");
                    continue;
                }

                var mainDoorId = availableOrdinaryDoors[nextOrdinaryIndex];
                nextOrdinaryIndex++;

                curation.Pairings.Add(new CuratedConnectionPairing
                {
                    DoorwayIdA = mainDoorId,
                    DoorwayIdB = pocket.EntranceDoorwayId,
                    Kind = ConnectionKind.NonEuclideanHidden
                });
                referencedDoorwayIds.Add(mainDoorId);
                referencedDoorwayIds.Add(pocket.EntranceDoorwayId);
            }
        }

        public static void BakeConnections(Layout layout, LevelConnectionCuration curation,
            NonEuclideanTransitionDefaults transitionDefaults)
        {
            if (curation.Seed != layout.Seed)
            {
                Debug.LogError($"[LevelGeneration] Curation asset targets seed {curation.Seed}, " +
                                $"but the current layout is seed {layout.Seed} — aborting.");
                return;
            }

            var rootName = $"Level_{layout.Seed}_Root";
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.GetRootGameObjects().Any(r => r.name == rootName))
            {
                Debug.LogError($"[LevelGeneration] '{rootName}' does not exist in the active scene — run Bake first.");
                return;
            }

            var root = FindOrCreateRoot(rootName); // safe now — existence already confirmed above

            foreach (var existingTrigger in root.GetComponentsInChildren<DoorwayTeleportTrigger>(true))
                Undo.DestroyObjectImmediate(existingTrigger);

            var allMarkers = root.GetComponentsInChildren<DoorwayMarker>(true)
                .ToDictionary(m => m.DoorwayId, m => m);

            var failures = new List<string>();
            foreach (var pairing in curation.Pairings)
            {
                // A null DoorwayId (e.g. an Isolated Pocket door that generation
                // itself couldn't place — SCARAB-014, see PlacePocketDoor)
                // is "not found", not a Dictionary key lookup to crash on.
                DoorwayMarker markerA = null, markerB = null;
                if (!string.IsNullOrEmpty(pairing.DoorwayIdA)) allMarkers.TryGetValue(pairing.DoorwayIdA, out markerA);
                if (!string.IsNullOrEmpty(pairing.DoorwayIdB)) allMarkers.TryGetValue(pairing.DoorwayIdB, out markerB);
                if (markerA == null) failures.Add($"{pairing.DoorwayIdA ?? "(none)"} not found");
                if (markerB == null) failures.Add($"{pairing.DoorwayIdB ?? "(none)"} not found");
                if (markerA == null || markerB == null) continue;

                // Purely for validation/logging — confirms both DoorwayIds are real
                // connections for this seed and reports which rooms they belonged to,
                // since Kind itself always comes from curation, never from Layout.
                LogPairing(layout, pairing);

                var triggerA = Undo.AddComponent<DoorwayTeleportTrigger>(markerA.gameObject);
                var triggerB = Undo.AddComponent<DoorwayTeleportTrigger>(markerB.gameObject);
                triggerA.Kind = pairing.Kind;
                triggerB.Kind = pairing.Kind;
                triggerA.PairedTrigger = triggerB;
                triggerB.PairedTrigger = triggerA;
                triggerA.TransitionDefaults = transitionDefaults;
                triggerB.TransitionDefaults = transitionDefaults;
            }

            if (failures.Count > 0)
            {
                Debug.LogError($"[LevelGeneration] Bake Connections: {failures.Count} bad row(s), " +
                                $"nothing else applied:\n{string.Join("\n", failures)}");
                return;
            }

            ValidatePocketReachability(layout, curation);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        // SCARAB-014: flags any Isolated Pocket that curation.Pairings doesn't
        // actually connect back to the main graph — a pure Debug.LogWarning,
        // no scene mutation, so it never needs its own idempotency story
        // alongside BakeConnections' reset-then-reapply trigger logic above.
        // A real graph walk (fixpoint iteration through Pairings), not a
        // per-door presence check — the latter would miss a closed loop of
        // Pockets curated only to each other, or a multi-hop chain, since a
        // door's id can appear in a Pairing row without that Pairing's other
        // side ever actually reaching the main graph.
        private static void ValidatePocketReachability(Layout layout, LevelConnectionCuration curation)
        {
            if (layout.Pockets.Count == 0) return;

            // Every DoorwayId's owning room(s) — both sides for an ordinary
            // door (it's one shared physical doorway), just the one room for a
            // Spare/Pocket door (ToRoomId is null: void on the other side).
            var roomsByDoorwayId = new Dictionary<string, List<string>>();
            foreach (var c in layout.Connections)
            {
                if (!roomsByDoorwayId.TryGetValue(c.DoorwayId, out var rooms))
                    roomsByDoorwayId[c.DoorwayId] = rooms = new List<string>();
                rooms.Add(c.FromRoomId);
                if (c.ToRoomId != null) rooms.Add(c.ToRoomId);
            }

            var reached = new HashSet<string>();
            foreach (var room in layout.Rooms)
                if (!layout.Pockets.Exists(p => p.RoomIds.Contains(room.RoomId)))
                    reached.Add(room.RoomId);

            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var pairing in curation.Pairings)
                {
                    if (!roomsByDoorwayId.TryGetValue(pairing.DoorwayIdA, out var roomsA)) continue;
                    if (!roomsByDoorwayId.TryGetValue(pairing.DoorwayIdB, out var roomsB)) continue;

                    bool aReached = roomsA.Exists(reached.Contains);
                    bool bReached = roomsB.Exists(reached.Contains);
                    if (aReached == bReached) continue; // both already reached, or neither — nothing new to spread

                    foreach (var roomId in aReached ? roomsB : roomsA)
                    {
                        if (!reached.Add(roomId)) continue;
                        changed = true;

                        // Reaching one room of a Pocket reaches all of it —
                        // internal connectivity between its own rooms is
                        // already guaranteed by generation (AssignPocketDoors).
                        var pocket = layout.Pockets.Find(p => p.RoomIds.Contains(roomId));
                        if (pocket == null) continue;
                        foreach (var pocketRoomId in pocket.RoomIds)
                            if (reached.Add(pocketRoomId))
                                changed = true;
                    }
                }
            }

            foreach (var pocket in layout.Pockets)
                if (!pocket.RoomIds.TrueForAll(reached.Contains))
                    Debug.LogWarning($"[LevelGeneration] Pocket {pocket.PocketId} is not reachable from the main " +
                                      "graph — check its Entrance/Exit curation.");
        }

        private static void LogRoomShellReplayReport(RoomShellReplayReport report)
        {
            if (report.TweaksApplied > 0 || report.DeletesApplied > 0 || report.AddsApplied > 0)
                Debug.Log($"[LevelGeneration] Room Shell reconciled: {report.TweaksApplied} tweak(s), " +
                           $"{report.DeletesApplied} delete(s) kept, {report.AddsApplied} add(s) restored.");

            foreach (var orphan in report.OrphansDropped)
                Debug.LogWarning("[LevelGeneration] Room Shell hand edit no longer matches generated " +
                                  $"content, dropped: {orphan}");
        }

        private static void LogPairing(Layout layout, CuratedConnectionPairing pairing)
        {
            string DescribeDoorway(string doorwayId)
            {
                var connection = layout.Connections.Find(c => c.DoorwayId == doorwayId);
                return connection != null
                    ? $"{doorwayId} ({connection.FromRoomId} <-> {connection.ToRoomId})"
                    : $"{doorwayId} (not a connection in seed {layout.Seed}'s layout)";
            }

            Debug.Log($"[LevelGeneration] Bake Connections: {DescribeDoorway(pairing.DoorwayIdA)} now paired " +
                       $"with {DescribeDoorway(pairing.DoorwayIdB)} as {pairing.Kind}.");
        }

        // Wall/door pieces are computed once across the whole Layout (a shared
        // gap cell between two rooms is one physical object, never duplicated),
        // then distributed to their owning Room GameObject after the fact.
        private static void AssembleAndDistributeShells(Layout layout, ShellAssemblyConfig shellConfig,
            Dictionary<string, GameObject> roomGoById, GameObject levelRoot)
        {
            var cellByInstance = RoomShellAssembler.BuildShells(layout, shellConfig, null);

            var cellOwner = new Dictionary<Vector2Int, string>();
            foreach (var room in layout.Rooms)
            foreach (var cell in room.FootprintCells)
                cellOwner[cell] = room.RoomId;

            foreach (var (instance, cell) in cellByInstance)
            {
                if (instance == null) continue;

                var owningRoomId = ResolveOwningRoom(cell, cellOwner);
                var owningGo = owningRoomId != null && roomGoById.TryGetValue(owningRoomId, out var go) ? go : levelRoot;
                instance.transform.SetParent(owningGo.transform, true);
            }
        }

        // wallCells (RoomShellAssembler.BuildShells) are detected 8-way, but a
        // 4-way-only search here missed any wall cell whose sole owned
        // neighbor is diagonal — it fell through to `owningRoomId == null`
        // and got parented directly under levelRoot instead of a room folder
        // (the "ungrouped prefabs" symptom, SCARAB-MAP-01 follow-up,
        // 2026-09-03). Every wallCell has at least one 8-way owned neighbor
        // by construction, so falling back to the diagonals guarantees an
        // owner is always found. 4-way is tried first and alone decides ties
        // when it finds anything, preserving prior behavior for the common
        // (non-diagonal-only) case.
        private static string ResolveOwningRoom(Vector2Int wallCell, Dictionary<Vector2Int, string> cellOwner)
        {
            string owner = FindClosestOwner(wallCell, Dirs4, cellOwner);
            return owner ?? FindClosestOwner(wallCell, DiagonalDirs4, cellOwner);
        }

        private static string FindClosestOwner(Vector2Int wallCell, Vector2Int[] dirs,
            Dictionary<Vector2Int, string> cellOwner)
        {
            string owner = null;
            foreach (var dir in dirs)
            {
                if (cellOwner.TryGetValue(wallCell + dir, out var roomId))
                    if (owner == null || string.CompareOrdinal(roomId, owner) < 0)
                        owner = roomId;
            }

            return owner;
        }

        private static void ClearGeneratedShellChildren(Transform parent,
            string placementGroup = RoomShellAssembler.WallPlacementGroup)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                var marker = child.GetComponent<PrefabBrushObject>();
                if (marker != null && marker.PlacementGroup == placementGroup)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static void AssignDefinition(RoomController controller, RoomDefinition definition)
        {
            var serialized = new SerializedObject(controller);
            var property = serialized.FindProperty("_definition");
            property.objectReferenceValue = definition;
            serialized.ApplyModifiedProperties();
        }

        private static void AddOrUpdateConnection(RoomDefinition from, RoomDefinition to, string doorwayId)
        {
            from.connections ??= new List<RoomConnection>();
            var existing = from.connections.Find(c => c.doorwayId == doorwayId);
            if (existing == null)
            {
                existing = new RoomConnection { doorwayId = doorwayId };
                from.connections.Add(existing);
            }

            existing.targetRoom = to;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace("\\", "/");
            var name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject FindOrCreateRoot(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name)
                    return root;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Bake Level");
            return go;
        }

        private static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Bake Level");
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
