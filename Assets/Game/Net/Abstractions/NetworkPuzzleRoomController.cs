using System.Collections.Generic;
using Game.Systems.LevelSystem.Runtime;
using Game.Systems.PuzzleSystem.Runtime;
using PurrNet;
using UnityEngine;

namespace Game.Net.Adapters
{
    /// <summary>
    /// Makes puzzle-solve confirmation network-authoritative for a room, without requiring
    /// each individual PuzzleController to have its own NetworkIdentity (PuzzleRoomController
    /// creates PuzzleControllers at runtime in Awake(), so they aren't stable network-spawned
    /// objects — the room root, a designed scene object, is).
    ///
    /// Every client still runs PuzzleController.Evaluate()/Solve() locally and immediately —
    /// that's what OnSolved reacts to for responsive local feedback (this is a co-op game with
    /// no anti-cheat requirement, so client-reported solves are trusted). This component's job
    /// is narrower: make sure that when multiple clients solve the same puzzle within the same
    /// network tick, exactly one confirmation reaches everyone, so reward-firing and
    /// progression (subscribed to PuzzleController.OnSolvedConfirmed) only happen once.
    /// </summary>
    [RequireComponent(typeof(PuzzleRoomController))]
    public class NetworkPuzzleRoomController : NetworkBehaviour
    {
        private PuzzleRoomController _room;
        private readonly HashSet<string> _confirmedSolvedIds = new();

        private void Awake()
        {
            _room = GetComponent<PuzzleRoomController>();
        }

        private void Start()
        {
            // PuzzleRoomController populates its puzzle list in its own Awake(), which is
            // guaranteed to have already run by the time our Start() fires.
            foreach (var puzzle in _room.PuzzleControllers)
                puzzle.OnSolved += HandleLocallySolved;
        }

        private void OnDestroy()
        {
            if (_room == null) return;
            foreach (var puzzle in _room.PuzzleControllers)
                puzzle.OnSolved -= HandleLocallySolved;
        }

        private void HandleLocallySolved(PuzzleController puzzle)
        {
            if (!isSpawned)
            {
                // No NetworkManager for this scene (e.g. a single-player-only level) —
                // confirm immediately rather than waiting for an RPC that will never arrive.
                puzzle.ConfirmSolved();
                return;
            }

            if (puzzle.Definition == null || string.IsNullOrEmpty(puzzle.Definition.puzzleId))
            {
                Debug.LogWarning($"[NetworkPuzzleRoomController] {puzzle.name} solved but has no " +
                    "PuzzleDefinition.puzzleId — cannot report it over the network. Confirming locally only.", this);
                puzzle.ConfirmSolved();
                return;
            }

            ReportSolvedRpc(puzzle.Definition.puzzleId);
        }

        [ServerRpc(requireOwnership: false)]
        private void ReportSolvedRpc(string puzzleId)
        {
            if (!_confirmedSolvedIds.Add(puzzleId)) return; // already confirmed — duplicate report from another client
            ConfirmSolvedRpc(puzzleId);
        }

        [ObserversRpc]
        private void ConfirmSolvedRpc(string puzzleId)
        {
            var puzzle = _room.FindPuzzleControllerById(puzzleId);
            if (puzzle == null)
            {
                Debug.LogWarning($"[NetworkPuzzleRoomController] Confirmed solve for unknown puzzleId '{puzzleId}'.", this);
                return;
            }
            puzzle.ConfirmSolved();
        }
    }
}
