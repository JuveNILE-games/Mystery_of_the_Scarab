using Core.Systems.InputManagement;
using UnityEngine;

namespace Game.Player{
    public class PlayerInputInitializer : MonoBehaviour{
        [SerializeField] private InputManager manager;
        [SerializeField] private bool enableActions = true;
        private InputReader _playerInputReader;
        
        public InputReader PlayerInputReader => _playerInputReader;
        public InputManager PlayerInputManager => manager;

        void Awake()
        {
            _playerInputReader = Instantiate(manager.inputReader);
            _playerInputReader.Actions = manager.inputReader.Actions;
            _playerInputReader.name = this.gameObject.name + " Input Reader";
        
            Debug.Log($"Initializing InputReader for {_playerInputReader.name}");
            if (enableActions)
            {
                _playerInputReader.Actions.Enable();
            }
            else
            {
                _playerInputReader.Actions.Disable();
            }
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
