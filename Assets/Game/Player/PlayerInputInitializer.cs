using Core.Systems.InputManagement;
using UnityEngine;

namespace Game.Player{
    public class PlayerInputInitializer : MonoBehaviour{
        [SerializeField] private InputManager manager;
        [SerializeField] private PlayerStateMachine stateMachine;
        [SerializeField] private bool enableActions = true;
        private InputReader _playerInputReader;
        
        public InputReader PlayerInputReader => _playerInputReader;
        public InputManager PlayerInputManager => manager;

        private void Awake()
        {
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();

            // Use the SHARED instance from InputManager
            // Do NOT instantiate a copy, or we won't get the events triggered by the system
            _playerInputReader = manager.inputReader;
            
            Debug.Log($"Linking InputReader: {_playerInputReader.name}");
            
            // Ensure it's initialized
            _playerInputReader.Initialize();
            
            SubscribeToInput();

            if (enableActions)
            {
                _playerInputReader.Actions.Enable();
            }
        }

        private void SubscribeToInput()
        {
            if (stateMachine == null) return;

            _playerInputReader.SubscribeStarted("Move", ctx => stateMachine.OnMove(ctx.ReadValue<Vector2>()));
            _playerInputReader.SubscribePerformed("Move", ctx => stateMachine.OnMove(ctx.ReadValue<Vector2>()));
            _playerInputReader.SubscribeCanceled("Move", ctx => stateMachine.OnMove(Vector2.zero));

            _playerInputReader.SubscribeStarted("Jump", ctx => stateMachine.OnJump(true));
            _playerInputReader.SubscribeCanceled("Jump", ctx => stateMachine.OnJump(false));

            _playerInputReader.SubscribeStarted("Sprint", ctx => stateMachine.OnSprint(true));
            _playerInputReader.SubscribeCanceled("Sprint", ctx => stateMachine.OnSprint(false));

            _playerInputReader.SubscribeStarted("PrimaryAbility", ctx => stateMachine.OnPrimaryAbility(true));
            _playerInputReader.SubscribeCanceled("PrimaryAbility", ctx => stateMachine.OnPrimaryAbility(false));

            _playerInputReader.SubscribeStarted("SecondaryAbility", ctx => stateMachine.OnSecondaryAbility(true));
            _playerInputReader.SubscribeCanceled("SecondaryAbility", ctx => stateMachine.OnSecondaryAbility(false));
        }

        private void OnDestroy()
        {
            // Do not Shutdown() the shared InputReader as it would kill input for everyone.
            // Ideally we should Unsubscribe specific callbacks here.
            // But since we use lambdas, we can't easily unsubscribe without refactoring.
            // For this session, we just avoid breaking the shared instance.
        }

        // Update is called once per frame
        void Update()
        {
        
        }
    }
}
