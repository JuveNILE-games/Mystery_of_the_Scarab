using Core.Systems.InputManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    public class PlayerInputInitializer : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private InputManager manager;
        [SerializeField] private PlayerStateMachine stateMachine;
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private PlayerAbilities abilities;

        [Header("Configuration")]
        [SerializeField] private bool enableActions = true;

        private InputReader _playerInputReader;
        
        public InputReader PlayerInputReader => _playerInputReader;
        public InputManager PlayerInputManager => manager;

        private void Awake()
        {
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
            if (abilities == null) abilities = GetComponent<PlayerAbilities>();

            // Link to the shared InputReader from the manager (or locate it if manager is missing)
            if (manager != null)
            {
                _playerInputReader = manager.inputReader;
            }
            else
            {
                // Fallback: try to find a global one or one on this object? 
                // For now, let's assume Manager is required as per previous setup.
                Debug.LogError("[PlayerInputInitializer] InputManager not assigned!");
                return;
            }
            
            Debug.Log($"Linking InputReader: {_playerInputReader.name}");
            
            // Ensure it's initialized
            _playerInputReader.Initialize();
        }

        private void OnEnable()
        {
            if (_playerInputReader != null)
            {
                SubscribeToInput();
                if (enableActions)
                {
                    _playerInputReader.Actions.Enable();
                }
            }
        }

        private void OnDisable()
        {
            if (_playerInputReader != null)
            {
                UnsubscribeFromInput();
            }
        }

        private void SubscribeToInput()
        {
            if (stateMachine == null) return;

            // Movement
            _playerInputReader.SubscribeStarted("Move", OnMove);
            _playerInputReader.SubscribePerformed("Move", OnMove);
            _playerInputReader.SubscribeCanceled("Move", OnMoveCanceled);

            // Jump
            _playerInputReader.SubscribeStarted("Jump", OnJumpStarted);
            _playerInputReader.SubscribeCanceled("Jump", OnJumpCanceled);

            // Sprint
            _playerInputReader.SubscribeStarted("Sprint", OnSprintStarted);
            _playerInputReader.SubscribeCanceled("Sprint", OnSprintCanceled);

            // Abilities
            _playerInputReader.SubscribeStarted("PrimaryAbility", OnPrimaryAbilityStarted);
            _playerInputReader.SubscribeCanceled("PrimaryAbility", OnPrimaryAbilityCanceled);

            _playerInputReader.SubscribeStarted("SecondaryAbility", OnSecondaryAbilityStarted);
            _playerInputReader.SubscribeCanceled("SecondaryAbility", OnSecondaryAbilityCanceled);

            // Interaction (Migrated from PlayerInputHandler)
            _playerInputReader.SubscribeStarted("Interact", OnInteractStarted);
        }

        private void UnsubscribeFromInput()
        {
            if (stateMachine == null) return;

            // Movement
            _playerInputReader.UnsubscribeStarted("Move", OnMove);
            _playerInputReader.UnsubscribePerformed("Move", OnMove);
            _playerInputReader.UnsubscribeCanceled("Move", OnMoveCanceled);

            // Jump
            _playerInputReader.UnsubscribeStarted("Jump", OnJumpStarted);
            _playerInputReader.UnsubscribeCanceled("Jump", OnJumpCanceled);

            // Sprint
            _playerInputReader.UnsubscribeStarted("Sprint", OnSprintStarted);
            _playerInputReader.UnsubscribeCanceled("Sprint", OnSprintCanceled);

            // Abilities
            _playerInputReader.UnsubscribeStarted("PrimaryAbility", OnPrimaryAbilityStarted);
            _playerInputReader.UnsubscribeCanceled("PrimaryAbility", OnPrimaryAbilityCanceled);

            _playerInputReader.UnsubscribeStarted("SecondaryAbility", OnSecondaryAbilityStarted);
            _playerInputReader.UnsubscribeCanceled("SecondaryAbility", OnSecondaryAbilityCanceled);

            // Interaction
            _playerInputReader.UnsubscribeStarted("Interact", OnInteractStarted);
        }

        #region Input Event Handlers

        private void OnMove(InputAction.CallbackContext ctx)
        {
            stateMachine.OnMove(ctx.ReadValue<Vector2>());
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            stateMachine.OnMove(Vector2.zero);
        }

        private void OnJumpStarted(InputAction.CallbackContext ctx) => stateMachine.OnJump(true);
        private void OnJumpCanceled(InputAction.CallbackContext ctx) => stateMachine.OnJump(false);

        private void OnSprintStarted(InputAction.CallbackContext ctx) => stateMachine.OnSprint(true);
        private void OnSprintCanceled(InputAction.CallbackContext ctx) => stateMachine.OnSprint(false);

        private void OnPrimaryAbilityStarted(InputAction.CallbackContext ctx)
        {
            stateMachine.OnPrimaryAbility(true);
            
            // Legacy/Direct component usage if needed (replaces PlayerInputHandler logic)
            if (abilities != null) abilities.GetContextualAbility()?.TryUse();
        }

        private void OnPrimaryAbilityCanceled(InputAction.CallbackContext ctx) => stateMachine.OnPrimaryAbility(false);

        private void OnSecondaryAbilityStarted(InputAction.CallbackContext ctx) => stateMachine.OnSecondaryAbility(true);
        private void OnSecondaryAbilityCanceled(InputAction.CallbackContext ctx) => stateMachine.OnSecondaryAbility(false);

        private void OnInteractStarted(InputAction.CallbackContext ctx)
        {
            // Migrated from PlayerInputHandler
            if (interactor != null)
            {
                interactor.TryLocalInteract();
            }
        }

        #endregion
    }
}
