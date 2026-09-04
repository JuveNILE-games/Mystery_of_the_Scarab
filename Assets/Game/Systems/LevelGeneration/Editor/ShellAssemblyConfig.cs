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
        public float CellSize = 2f;
    }
}
