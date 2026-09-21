using Core.Tools.PrefabBrush;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // Inputs SCARAB-011's Editor window is responsible for collecting from the
    // designer; RoomShellAssembler only consumes them.
    public class ShellAssemblyConfig
    {
        public PrefabRuleSet RuleSet;
        public GameObject DoorPrefab;
        public GameObject MultiRoomSeamPrefab;

        // Floor is a generated mesh (RoomFloorMeshBuilder), not a placed
        // prefab (Michael, 2026-09-04) — no tile prefab/mesh-import-scale to
        // keep in sync with CellSize at all.
        public Material FloorMaterial;
        public FloorUvMode FloorUvMode = FloorUvMode.ContinuousWorldSpace;

        // Extruded downward from the visible top surface (y=0, flush with
        // the wall base) — a flat zero-thickness plane looked wrong (Michael,
        // 2026-09-04: "effectively the floor is cube-like").
        public float FloorThickness = 0.2f;

        // The wall kit (WallPrefabRuleSet) is authored for 1-unit cells — a
        // WallStraightPrefab instance measures exactly 1.0 unit along its own
        // run axis (confirmed live, 2026-09-04/05). CellSize=2 places walls
        // twice their own width apart, leaving a real 1-unit gap in the wall
        // between every straight segment — confirmed live, 2026-09-05 (this
        // default's previous value of 2f was itself part of the bug, silently
        // wrong for any caller that didn't override it).
        public float CellSize = 1f;
    }
}
