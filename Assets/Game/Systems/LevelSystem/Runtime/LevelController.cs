using System;
using System.Collections.Generic;
using System.Linq;
using Game.Systems.LevelSystem.Definitions;
using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime{
    public class LevelController : MonoBehaviour
    {
        [SerializeField] private LevelDefinition _definition;

        private readonly Dictionary<string, RoomController> _rooms = new();
        private RoomController _currentRoom;

        public event Action<RoomController, RoomController> OnRoomChanged; // (from, to)
        public event Action OnLevelComplete;

        private void Start()
        {
            // Collect all RoomControllers in scene
            foreach (var rc in FindObjectsByType<RoomController>(FindObjectsSortMode.None))
                _rooms[rc.Definition.roomId] = rc;

            // Deactivate all rooms, then enter entry room
            foreach (var rc in _rooms.Values) rc.gameObject.SetActive(false);
            EnterRoom(_definition.entryRoom.roomId);
        }

        public void EnterRoom(string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var target)) return;

            var previous = _currentRoom;
            previous?.ExitRoom();

            _currentRoom = target;
            _currentRoom.EnterRoom();

            OnRoomChanged?.Invoke(previous, _currentRoom);
            CheckLevelComplete();
        }

        private void CheckLevelComplete()
        {
            if (_definition.requireAllPuzzleRoomsToExit)
            {
                var allSolved = _rooms.Values
                    .OfType<PuzzleRoomController>()
                    .All(pr => pr.IsRoomSolved);
                if (allSolved) OnLevelComplete?.Invoke();
            }
        }
    }
}