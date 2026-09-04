using Core.Tools.PrefabBrush;
using UnityEditor;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // The designer-facing counterpart to SCARAB-006's data mapping (SCARAB-008):
    // a plain [MenuItem] EditorWindow (no SceneView EditorTool — baking has no
    // interactive viewport component), an in-memory 2D room-graph preview that
    // costs nothing to re-roll, and a Bake button that's the only action
    // committing real assets/scene content.
    public class LevelGenerationWindow : EditorWindow
    {
        private const string PrefsPrefix = "LevelGeneration_";

        private GenerationParameters _parameters = new();
        private PrefabRuleSet _ruleSet;
        private GameObject _doorPrefab;
        private float _cellSize = 1f;
        private Layout _previewLayout;

        [MenuItem("Tools/Level Generation/Generate Level")]
        private static void Open()
        {
            GetWindow<LevelGenerationWindow>("Level Generation");
        }

        private void OnEnable() => LoadPendingState();

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Seed", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _parameters.Seed = EditorGUILayout.IntField("Seed", _parameters.Seed);
            if (GUILayout.Button("Randomize", GUILayout.Width(90)))
                _parameters.Seed = Random.Range(int.MinValue, int.MaxValue);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            _parameters.RoomCount = EditorGUILayout.IntSlider("Room Count", _parameters.RoomCount, 3, 20);
            _parameters.PuzzleRoomCount =
                EditorGUILayout.IntSlider("Puzzle Rooms", _parameters.PuzzleRoomCount, 0, _parameters.RoomCount);
            _parameters.OpenSpaceCount =
                EditorGUILayout.IntSlider("Open Spaces", _parameters.OpenSpaceCount, 0, _parameters.RoomCount);
            _parameters.MinEntryExitDistance =
                EditorGUILayout.IntSlider("Min Entry/Exit Distance", _parameters.MinEntryExitDistance, 1, _parameters.RoomCount);
            _parameters.LoopCount = EditorGUILayout.IntSlider("Loop Connections", _parameters.LoopCount, 0, 6);
            _parameters.CorridorCount =
                EditorGUILayout.IntSlider("Corridors", _parameters.CorridorCount, 0, _parameters.RoomCount);
            _parameters.CorridorMinLength =
                EditorGUILayout.IntSlider("Corridor Min Length", _parameters.CorridorMinLength, 1, 12);
            _parameters.CorridorMaxLength = EditorGUILayout.IntSlider("Corridor Max Length",
                Mathf.Max(_parameters.CorridorMaxLength, _parameters.CorridorMinLength), _parameters.CorridorMinLength, 16);
            _parameters.IrregularRoomCount =
                EditorGUILayout.IntSlider("Irregular Rooms", _parameters.IrregularRoomCount, 0, _parameters.RoomCount);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Room Shell", EditorStyles.boldLabel);
            _ruleSet = (PrefabRuleSet)EditorGUILayout.ObjectField("Rule Set", _ruleSet, typeof(PrefabRuleSet), false);
            _doorPrefab = (GameObject)EditorGUILayout.ObjectField("Door Prefab", _doorPrefab, typeof(GameObject), false);
            _cellSize = EditorGUILayout.FloatField("Cell Size", _cellSize);

            bool changed = EditorGUI.EndChangeCheck();
            if (changed || _previewLayout == null)
            {
                _previewLayout = BspLayoutGenerator.Generate(CloneParameters());
                SavePendingState();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            DrawPreview();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_ruleSet == null))
            {
                if (GUILayout.Button("Bake", GUILayout.Height(30)))
                    BakeLevel();
            }
        }

        private void BakeLevel()
        {
            var layout = BspLayoutGenerator.Generate(CloneParameters());
            var shellConfig = new ShellAssemblyConfig { RuleSet = _ruleSet, DoorPrefab = _doorPrefab, CellSize = _cellSize };
            var levelDef = LevelBaker.Bake(layout, shellConfig, $"Level {layout.Seed}");

            ClearPendingState();
            _previewLayout = layout;

            EditorGUIUtility.PingObject(levelDef);

            int requested = 0, corridors = 0, margins = 0;
            foreach (var room in layout.Rooms)
            {
                if (room.Role == RoomRole.Corridor) corridors++;
                else if (room.Role == RoomRole.Margin) margins++;
                else requested++;
            }

            // requested/corridors/margins broken out separately because
            // RoomCount only ever governs the first figure — corridors and
            // margin rooms are structural byproducts of carving corridors,
            // not something the designer dialed in, and silently folding
            // them into one flat "N rooms" total reads as a parameter
            // mismatch (SCARAB-MAP-01 follow-up, 2026-09-03).
            Debug.Log($"[LevelGeneration] Baked '{levelDef.displayName}' — {requested} rooms " +
                      $"(RoomCount was {_parameters.RoomCount}) + {corridors} corridor(s) + {margins} margin room(s), " +
                      $"{layout.Connections.Count} connections.");
        }

        private GenerationParameters CloneParameters() => new()
        {
            Seed = _parameters.Seed,
            RoomCount = _parameters.RoomCount,
            PuzzleRoomCount = _parameters.PuzzleRoomCount,
            OpenSpaceCount = _parameters.OpenSpaceCount,
            MinEntryExitDistance = _parameters.MinEntryExitDistance,
            LoopCount = _parameters.LoopCount,
            CorridorCount = _parameters.CorridorCount,
            CorridorMinLength = _parameters.CorridorMinLength,
            CorridorMaxLength = _parameters.CorridorMaxLength,
            IrregularRoomCount = _parameters.IrregularRoomCount
        };

        private void DrawPreview()
        {
            var rect = GUILayoutUtility.GetRect(position.width - 20, 240);
            GUI.Box(rect, GUIContent.none);

            if (_previewLayout == null || _previewLayout.Rooms.Count == 0) return;

            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var room in _previewLayout.Rooms)
            foreach (var cell in room.FootprintCells)
            {
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            float spanX = Mathf.Max(1, maxX - minX + 1);
            float spanY = Mathf.Max(1, maxY - minY + 1);
            float scale = Mathf.Min((rect.width - 20) / spanX, (rect.height - 20) / spanY);

            Vector2 ToPoint(Vector2Int cell) => new(
                rect.x + 10 + (cell.x - minX) * scale,
                rect.yMax - 10 - (cell.y - minY) * scale);

            Handles.BeginGUI();

            foreach (var connection in _previewLayout.Connections)
            {
                var from = _previewLayout.FindRoom(connection.FromRoomId);
                var to = _previewLayout.FindRoom(connection.ToRoomId);
                if (from == null || to == null) continue;

                Handles.color = connection.IsLoopEdge ? Color.yellow : Color.white;
                Handles.DrawLine(ToPoint(RoomCenter(from)), ToPoint(RoomCenter(to)));
            }

            foreach (var room in _previewLayout.Rooms)
            {
                var color = room.Role switch
                {
                    RoomRole.Puzzle => new Color(0.6f, 0.3f, 0.9f),
                    RoomRole.OpenSpace => new Color(0.3f, 0.6f, 0.9f),
                    RoomRole.Corridor => new Color(0.5f, 0.5f, 0.5f),
                    RoomRole.Margin => new Color(0.35f, 0.35f, 0.3f),
                    _ => new Color(0.75f, 0.75f, 0.35f)
                };
                if (room.IsEntry) color = Color.green;
                if (room.IsExit) color = Color.red;

                foreach (var cell in room.FootprintCells)
                {
                    var p = ToPoint(cell);
                    Handles.DrawSolidRectangleWithOutline(
                        new Rect(p.x - scale * 0.45f, p.y - scale * 0.45f, scale * 0.9f, scale * 0.9f),
                        color, Color.black);
                }
            }

            Handles.EndGUI();
        }

        private static Vector2Int RoomCenter(LayoutRoomNode room)
        {
            long sx = 0, sy = 0;
            foreach (var c in room.FootprintCells)
            {
                sx += c.x;
                sy += c.y;
            }

            int n = Mathf.Max(1, room.FootprintCells.Count);
            return new Vector2Int((int)(sx / n), (int)(sy / n));
        }

        // EditorPrefs persistence is scoped to uncommitted preview state only
        // (SCARAB-008) — cleared the moment Bake actually commits real assets.
        private void SavePendingState()
        {
            EditorPrefs.SetInt(PrefsPrefix + "Seed", _parameters.Seed);
            EditorPrefs.SetInt(PrefsPrefix + "RoomCount", _parameters.RoomCount);
            EditorPrefs.SetInt(PrefsPrefix + "PuzzleRoomCount", _parameters.PuzzleRoomCount);
            EditorPrefs.SetInt(PrefsPrefix + "OpenSpaceCount", _parameters.OpenSpaceCount);
            EditorPrefs.SetInt(PrefsPrefix + "MinEntryExitDistance", _parameters.MinEntryExitDistance);
            EditorPrefs.SetInt(PrefsPrefix + "LoopCount", _parameters.LoopCount);
            EditorPrefs.SetInt(PrefsPrefix + "CorridorCount", _parameters.CorridorCount);
            EditorPrefs.SetInt(PrefsPrefix + "CorridorMinLength", _parameters.CorridorMinLength);
            EditorPrefs.SetInt(PrefsPrefix + "CorridorMaxLength", _parameters.CorridorMaxLength);
            EditorPrefs.SetInt(PrefsPrefix + "IrregularRoomCount", _parameters.IrregularRoomCount);
            EditorPrefs.SetFloat(PrefsPrefix + "CellSize", _cellSize);
            EditorPrefs.SetString(PrefsPrefix + "RuleSet", _ruleSet != null ? AssetDatabase.GetAssetPath(_ruleSet) : "");
            EditorPrefs.SetString(PrefsPrefix + "DoorPrefab", _doorPrefab != null ? AssetDatabase.GetAssetPath(_doorPrefab) : "");
            EditorPrefs.SetBool(PrefsPrefix + "HasPending", true);
        }

        private void LoadPendingState()
        {
            if (!EditorPrefs.GetBool(PrefsPrefix + "HasPending", false)) return;

            _parameters.Seed = EditorPrefs.GetInt(PrefsPrefix + "Seed", 0);
            _parameters.RoomCount = EditorPrefs.GetInt(PrefsPrefix + "RoomCount", 8);
            _parameters.PuzzleRoomCount = EditorPrefs.GetInt(PrefsPrefix + "PuzzleRoomCount", 2);
            _parameters.OpenSpaceCount = EditorPrefs.GetInt(PrefsPrefix + "OpenSpaceCount", 1);
            _parameters.MinEntryExitDistance = EditorPrefs.GetInt(PrefsPrefix + "MinEntryExitDistance", 3);
            _parameters.LoopCount = EditorPrefs.GetInt(PrefsPrefix + "LoopCount", 2);
            _parameters.CorridorCount = EditorPrefs.GetInt(PrefsPrefix + "CorridorCount", 2);
            _parameters.CorridorMinLength = EditorPrefs.GetInt(PrefsPrefix + "CorridorMinLength", 4);
            _parameters.CorridorMaxLength = EditorPrefs.GetInt(PrefsPrefix + "CorridorMaxLength", 8);
            _parameters.IrregularRoomCount = EditorPrefs.GetInt(PrefsPrefix + "IrregularRoomCount", 2);
            _cellSize = EditorPrefs.GetFloat(PrefsPrefix + "CellSize", 1f);

            var ruleSetPath = EditorPrefs.GetString(PrefsPrefix + "RuleSet", "");
            if (!string.IsNullOrEmpty(ruleSetPath))
                _ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(ruleSetPath);

            var doorPath = EditorPrefs.GetString(PrefsPrefix + "DoorPrefab", "");
            if (!string.IsNullOrEmpty(doorPath))
                _doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(doorPath);
        }

        private void ClearPendingState()
        {
            EditorPrefs.DeleteKey(PrefsPrefix + "HasPending");
        }
    }
}
