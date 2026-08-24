using System.Collections.Generic;
using Game.Systems.LevelSystem.Runtime;
using Game.Systems.PuzzleSystem.Runtime;
using PurrNet;
using UnityEngine;

namespace Game.Net.Adapters
{
    /// <summary>
    /// Makes puzzle-solve confirmation network-authoritative for a room: every client solves
    /// locally for responsive feedback, this ensures exactly one confirmation reaches everyone.
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
