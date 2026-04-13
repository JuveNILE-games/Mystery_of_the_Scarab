using System;
using System.Collections.Generic;
using Game.Systems.PuzzleSystem.Definitions;
using UnityEngine;

namespace Game.Systems.LevelSystem.Definitions{
    [CreateAssetMenu(menuName = "JuveNILE Games/Mystery of the Scarab/Level System/Rooms/Room Definition")]
    public class RoomDefinition : ScriptableObject{
        [Header("Identity")] public string roomId;
        public string displayName;

        [Header("Scene Object")]
        // Tag or name of the root GameObject that represents this room in the level scene.
        public string sceneRootTag;

        [Header("Connections")]
        // Which rooms can be reached from here (doors/passages).
        public List<RoomConnection> connections;
    }

    [Serializable]
    public class RoomConnection{
        public RoomDefinition targetRoom;
        public string doorwayId; // matches a DoorwayMarker in the scene
        public bool lockedByDefault;
        public List<PuzzleDefinition> unlockedBy; // empty = always open
    }
}