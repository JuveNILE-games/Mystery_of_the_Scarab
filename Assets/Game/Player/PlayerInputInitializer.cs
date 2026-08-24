using Core.Systems.InputManagement;
using NetCore.Abstractions;
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

        // Null for non-networked players (single-player, local multiplayer).
        private INetworkOwnershipGate _ownershipGate;

        private InputReader _playerInputReader;
        // Cached camera reference for world-space projection. The camera is looked up once in
        // Awake and re-cached on first use if it somehow changes (e.g. scene load). All input
        // events run through ProjectToWorldSpace() so states never need Camera.main.
        private Camera _camera;
        
        public InputReader PlayerInputReader => _playerInputReader;
        public InputManager PlayerInputManager => manager;

        private void Awake()
        {
            if (stateMachine == null) stateMachine = GetComponent<PlayerStateMachine>();
            if (interactor == null) interactor = GetComponent<PlayerInteractor>();
            if (abilities == null) abilities = GetComponent<PlayerAbilities>();
            if (_ownershipGate == null) _ownershipGate = GetComponent<INetworkOwnershipGate>();

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
            
            // Cache camera once. Re-cached lazily in ProjectToWorldSpace if null.
            _camera = Camera.main;

            // Ensure it's initialized
            _playerInputReader.Initialize();
        }

        private void OnEnable()
        {
            // A non-owner networked player should never process local input.
            if (_ownershipGate != null && !_ownershipGate.CanAcceptLocalControl)
            {
                ClearInputState();
                return;
            }

            if (_playerInputReader != null)
            {
                SubscribeToInput();
                if (enableActions)
                {
                    // Use SetInputMode to ensure:
                    // 1. Only 'Player' map is enabled (exclusive)
                    // 2. InputReader.CurrentMode is updated to 'Gameplay'
                    _playerInputReader.SetInputMode(InputReader.InputMode.Gameplay);
                }
            }
        }

        private void OnDisable()
        {
            if (_playerInputReader != null)
            {
                UnsubscribeFromInput();
                ClearInputState();
            }
        }

        public void ClearInputState()
        {
            if (stateMachine != null)
            {
                stateMachine.OnMoveWorldSpace(Vector3.zero);
                stateMachine.OnJump(false);
                stateMachine.OnSprint(false);
                stateMachine.OnPrimaryAbility(false);
                stateMachine.OnSecondaryAbility(false);
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
            var raw = ctx.ReadValue<Vector2>();
            stateMachine.OnMoveWorldSpace(ProjectToWorldSpace(raw));
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            stateMachine.OnMoveWorldSpace(Vector3.zero);
        }

        /// <summary>
        /// Projects a camera-relative 2D input vector onto the world XZ plane. Called only on
        /// input events, not every frame.
        /// </summary>
        private Vector3 ProjectToWorldSpace(Vector2 input)
        {
            if (input.sqrMagnitude < 0.01f) return Vector3.zero;

            // Lazy re-cache in case the camera was swapped (e.g. after a scene transition).
            if (_camera == null) _camera = Camera.main;

            Vector3 camForward = _camera != null ? _camera.transform.forward : Vector3.forward;
            Vector3 camRight   = _camera != null ? _camera.transform.right   : Vector3.right;

            // Flatten to XZ — we don't want camera pitch to affect ground movement direction.
            camForward.y = 0f;
            camRight.y   = 0f;
            camForward.Normalize();
            camRight.Normalize();

            // input.y = forward axis (W/S / stick up-down)
            // input.x = lateral axis (A/D / stick left-right)
            Vector3 worldDir = camForward * input.y + camRight * input.x;

            // Normalize so diagonal movement isn't faster.
            // Analog sticks: raw magnitude represents push intensity; normalize so
            // walk speed is consistent. Analog sensitivity can be added to CharacterData later.
            return worldDir.sqrMagnitude > 0.01f ? worldDir.normalized : Vector3.zero;
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
