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
    public class PuzzleRoomController : RoomController{
        public new PuzzleRoomDefinition Definition => (PuzzleRoomDefinition)_definition;

        public bool IsRoomSolved { get; private set; }
        public event Action<PuzzleRoomController> OnRoomSolved;
        public event Action<PuzzleController> OnPuzzleUnlocked;

        private readonly List<PuzzleController> _puzzleControllers = new();
        private PuzzleRewardExecutor _rewardExecutor;

        private void Awake(){
            _rewardExecutor = FindFirstObjectByType<PuzzleRewardExecutor>();

            // Find or create PuzzleControllers for each PuzzleDefinition.
            foreach (var puzzleDef in Definition.puzzles)
            {
                // Look for an existing PuzzleController in children that already holds this def.
                var existing = FindPuzzleController(puzzleDef);
                if (existing == null)
                {
                    var go = new GameObject($"Puzzle_{puzzleDef.puzzleId}");
                    go.transform.SetParent(transform);
                    existing = go.AddComponent<PuzzleController>();
                    existing.Initialize(puzzleDef);
                }

                _puzzleControllers.Add(existing);
                existing.OnSolved += OnPuzzleSolved;
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
                    {
                        prereq.OnSolved += _ => {
                            pc.Initialize();
                            OnPuzzleUnlocked?.Invoke(pc);
                        };
                    }
                }
            }
        }

        private void OnPuzzleSolved(PuzzleController puzzle)
        {
            // Fire rewards
            if (_rewardExecutor != null)
            {
                _rewardExecutor.Execute(puzzle.Definition.onSolvedRewards);
            }

            EvaluateRoomSolved();
        }

        private void EvaluateRoomSolved(){
            bool solved = Definition.solveMode == RoomSolveMode.All
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
            if (Definition.resetPuzzlesOnExit)
                foreach (var pc in _puzzleControllers)
                    pc.ResetPuzzle();
        }

        private PuzzleController FindPuzzleController(PuzzleDefinition def)
            => GetComponentsInChildren<PuzzleController>()
                .FirstOrDefault(pc => pc.Definition == def);

        public IEnumerable<PuzzleController> GetUnsolvedPuzzles()
            => _puzzleControllers.Where(p => !p.IsSolved && !p.IsFailed);
    }
}