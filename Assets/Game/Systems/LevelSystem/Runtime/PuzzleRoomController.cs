using System;
using System.Collections.Generic;
using System.Linq;
using Game.Systems.LevelSystem.Definitions;
using Game.Systems.PuzzleSystem.Definitions;
using Game.Systems.PuzzleSystem.Runtime;
using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime{
    /// <summary>
    /// Attached to the room root GameObject. Owns all PuzzleControllers in the room.
    /// Reads PuzzleRoomDefinition at runtime; at design-time the definition is assigned in Inspector.
    /// </summary>
    public class PuzzleRoomController : MonoBehaviour{
        [SerializeField] private PuzzleRoomDefinition _definition;

        public bool IsRoomSolved { get; private set; }
        public event Action<PuzzleRoomController> OnRoomSolved;

        private readonly List<PuzzleController> _puzzleControllers = new();

        private void Awake(){
            // Find or create PuzzleControllers for each PuzzleDefinition.
            foreach (var puzzleDef in _definition.puzzles)
            {
                // Look for an existing PuzzleController in children that already holds this def.
                var existing = FindPuzzleController(puzzleDef);
                if (existing == null)
                {
                    var go = new GameObject($"Puzzle_{puzzleDef.puzzleId}");
                    go.transform.SetParent(transform);
                    existing = go.AddComponent<PuzzleController>();
                    // Assign via reflection or expose a public Init method:
                    existing.Initialize(puzzleDef); // see note below
                }

                _puzzleControllers.Add(existing);
                existing.OnSolved += _ => EvaluateRoomSolved();
            }

            HandlePrerequisites();
        }

        private void HandlePrerequisites(){
            foreach (var pc in _puzzleControllers)
            {
                if (pc.Definition.prerequisite != null)
                {
                    var prereq = _puzzleControllers.Find(p => p.Definition == pc.Definition.prerequisite);
                    if (prereq != null)
                        prereq.OnSolved += _ => pc.Initialize();
                    // Lock the dependent puzzle until prereq fires
                }
            }
        }

        private void EvaluateRoomSolved(){
            bool solved = _definition.solveMode == RoomSolveMode.All
                ? _puzzleControllers.All(p => p.IsSolved)
                : _puzzleControllers.Any(p => p.IsSolved);

            if (solved && !IsRoomSolved)
            {
                IsRoomSolved = true;
                OnRoomSolved?.Invoke(this);
            }
        }

        public void ResetRoom(){
            IsRoomSolved = false;
            if (_definition.resetPuzzlesOnExit)
                foreach (var pc in _puzzleControllers)
                    pc.ResetPuzzle();
        }

        private PuzzleController FindPuzzleController(PuzzleDefinition def)
            => GetComponentsInChildren<PuzzleController>()
                .FirstOrDefault(pc => pc.Definition == def);
    }
}