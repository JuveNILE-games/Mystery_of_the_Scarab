using System.Collections.Generic;
using System.IO;
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

            var roomDefsById = new Dictionary<string, RoomDefinition>();
            var roomGoById = new Dictionary<string, GameObject>();

            foreach (var roomNode in layout.Rooms)
            {
                var (roomDef, roomGo) = BakeRoomShell(folder, roomNode, levelRoot.transform);
                if (roomDef != null) roomDefsById[roomNode.RoomId] = roomDef;
                roomGoById[roomNode.RoomId] = roomGo;
            }

            AssembleAndDistributeShells(layout, shellConfig, roomGoById, levelRoot);

            foreach (var connection in layout.Connections)
            {
                AddOrUpdateConnection(roomDefsById[connection.FromRoomId], roomDefsById[connection.ToRoomId], connection.DoorwayId);
                AddOrUpdateConnection(roomDefsById[connection.ToRoomId], roomDefsById[connection.FromRoomId], connection.DoorwayId);
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

        private static void ClearGeneratedShellChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                var marker = child.GetComponent<PrefabBrushObject>();
                if (marker != null && marker.PlacementGroup == "LevelGeneration")
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
