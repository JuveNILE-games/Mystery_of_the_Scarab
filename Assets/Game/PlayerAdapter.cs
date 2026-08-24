using UnityEngine;
using Core;
using Core.Utility.Attributes;
using NetCore.Abstractions;

namespace Game.Player
{
    /// <summary>
    /// Adapter for the player character that allows it to be switched between 
    /// direct player control and AI companion control.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(Game.DialogueParticipant))]
    public class PlayerAdapter : MonoBehaviour, IControllable
    {
        [Header("Components")]
        [SerializeField] private PlayerInputInitializer _input;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private PlayerAbilities _abilities;

        private IAIController _aiController;
        private INetworkOwnershipGate _ownershipGate;

        [Inject] private IControllableRegistry _registry;

        private void Awake()
        {
            if (_input == null) _input = GetComponent<PlayerInputInitializer>();
            if (_interactor == null) _interactor = GetComponent<PlayerInteractor>();
            if (_abilities == null) _abilities = GetComponent<PlayerAbilities>();
            if (_aiController == null) _aiController = GetComponent<IAIController>();
            // Network components live on this same GameObject, not a child — PurrNet's Spawn()
            // only registers the exact GameObject it's called on.
            if (_ownershipGate == null)
                _ownershipGate = GetComponent<INetworkOwnershipGate>();
        }

        private void Start()
        {
            // Initial registration - runs after framework injection pass
            _registry?.Register(this);
        }

        private void OnEnable()
        {
            // Subsequent enable cycles - _registry is already set by this point
            _registry?.Register(this);
        }

        private void OnDisable()
        {
            _registry?.Unregister(this);
        }

        public Transform GetTransform() => transform;

        public void OnControlGained()
        {
            if (!CanAcceptLocalControl())
            {
                if (_input != null) _input.enabled = false;
                if (_interactor != null) _interactor.SetControlled(false);
                if (_abilities != null) _abilities.OnControlLost();
                return;
            }

            // Enable direct player input
            if (_input != null) _input.enabled = true;
            
            // Disable AI behavior
            if (_aiController != null) _aiController.EnableAI(false);

            // Update interactor and abilities
            if (_interactor != null) _interactor.SetControlled(true);
            if (_abilities != null) _abilities.OnControlGained();
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PlayerAdapter] Direct control gained for {gameObject.name}");
#endif
        }

        public void OnControlLost()
        {
            // Disable direct player input
            if (_input != null) _input.enabled = false;
            
            // Enable AI behavior (companion mode)
            if (_aiController != null) _aiController.EnableAI(true);

            // Update interactor and abilities
            if (_interactor != null) _interactor.SetControlled(false);
            if (_abilities != null) _abilities.OnControlLost();
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PlayerAdapter] Direct control lost for {gameObject.name} (AI enabled)");
#endif
        }

        private bool CanAcceptLocalControl()
        {
            return _ownershipGate == null || _ownershipGate.CanAcceptLocalControl;
        }
    }
}
