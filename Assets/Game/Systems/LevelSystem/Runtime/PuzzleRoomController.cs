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

        /// <summary>Read-only view for NetworkPuzzleRoomController to subscribe/look up by id.</summary>
        public IReadOnlyList<PuzzleController> PuzzleControllers => _puzzleControllers;

        public PuzzleController FindPuzzleControllerById(string puzzleId)
            => _puzzleControllers.Find(p => p.Definition != null && p.Definition.puzzleId == puzzleId);

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
                // Confirmed, not raw OnSolved: rewards/progression are real game-state side
                // effects, and OnSolved alone can fire independently on every client (no
                // network authority) — see NetworkPuzzleRoomController.
                existing.OnSolvedConfirmed += OnPuzzleSolved;
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
                        prereq.OnSolvedConfirmed += _ => {
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
                ? _puzzleControllers.All(p => p.IsSolveConfirmed)
                : _puzzleControllers.Any(p => p.IsSolveConfirmed);

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