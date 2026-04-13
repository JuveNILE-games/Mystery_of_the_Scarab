using System.Collections.Generic;
using Game.Systems.PuzzleSystem.Definitions;
using UnityEngine;

namespace Game.Systems.LevelSystem.Definitions{
    [CreateAssetMenu(menuName = "JuveNILE Games/Mystery of the Scarab/Level System/Rooms/Puzzle Room Definition")]
    public class PuzzleRoomDefinition : RoomDefinition{
        [Header("Puzzles")] public List<PuzzleDefinition> puzzles;

        [Header("Room-level behaviour")]
        // Room is "solved" when ALL puzzles are solved.
        // OR override: room is solved when ANY puzzle is solved.
        public RoomSolveMode solveMode; // All | Any

        public bool resetPuzzlesOnExit;
    }

    public enum RoomSolveMode{
        All,
        Any
    }
}