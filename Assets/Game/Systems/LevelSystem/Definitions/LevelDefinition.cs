using System.Collections.Generic;
using Core.Systems.AudioSystem;
using Core.Systems.Dialogue;
using Eflatun.SceneReference;
using UnityEngine;

namespace Game.Systems.LevelSystem.Definitions
{
    [CreateAssetMenu(menuName = "JuveNILE Games/Mystery of the Scarab/Level System/Level Definition")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string levelId;
        public string displayName;

        [Header("Scene")]
        // The Unity scene that contains all room GameObjects for this level.
        public SceneReference levelScene;  // use the existing SceneReference type in Core

        [Header("Rooms")]
        public List<RoomDefinition> rooms;
        public RoomDefinition entryRoom;

        [Header("Flow")]
        // Optional: completing all puzzleRooms unlocks this exit.
        public RoomDefinition exitRoom;
        public bool requireAllPuzzleRoomsToExit;

        [Header("Narrative")]
        public DialogueReference onLevelEnterDialogue;
        public DialogueReference onLevelCompleteDialogue;
        public SoundData backgroundMusic;
    }
}
