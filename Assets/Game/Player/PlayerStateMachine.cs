using Core.Systems.StateMachine;
using Game.Player.States;
using Game.Player.States.Ability;
using Game.Player.States.Grounded;
using SpriteAnimations;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Player state machine that manages all player states and transitions.
    /// Hierarchical structure:
    /// - Grounded (Idle, Walk, Sprint, Jump, Land)
    /// - Airborne (Falling)
    /// - Ability (PrimaryAbility, SecondaryAbility)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerStateMachine : StateMachineComponent<PlayerStateMachine, BaseState<PlayerStateMachine, PlayerState>>
    {
        [Header("Component References")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private SpriteAnimator anim;
        
        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private Vector3 groundCheckOffset = Vector3.zero;
        
        [Header("Input (Connect from InputSystem)")]
        [SerializeField] private Vector2 moveInput;
        [SerializeField] private bool jumpPressed;
        [SerializeField] private bool sprintPressed;
        [SerializeField] private bool primaryAbilityPressed;
        [SerializeField] private bool secondaryAbilityPressed;
        
        // State management
        private GroundedState groundedState;
        private AirborneState airborneState;
        private AbilityState abilityState;
        
        // Ability tracking
        private bool primaryAbilityFinished;
        private bool secondaryAbilityFinished;
        
        // Grounded check cache
        private bool isGrounded;
        
        #region Public Properties (accessed by states)
        
        public Rigidbody Rigidbody => rb;
        public SpriteAnimator Animator => anim;
        public Vector2 MoveInput => moveInput;
        public bool IsGrounded => isGrounded;
        public bool IsJumpPressed => jumpPressed;
        public bool IsSprintPressed => sprintPressed;
        public bool IsPrimaryAbilityPressed => primaryAbilityPressed;
        public bool IsSecondaryAbilityPressed => secondaryAbilityPressed;
        
        #endregion
        
        #region Unity Lifecycle
        
        protected override void Awake()
        {
            // Cache components
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
            
            base.Awake();
        }
        
        private void OnValidate()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
        }
        
        protected override void Update()
        {
            base.Update();
            
            // Update grounded state
            CheckGrounded();
            
            // Handle top-level state transitions
            HandleMainStateTransitions();
        }
        
        #endregion
        
        #region State Machine Setup
        
        protected override void RegisterStates()
        {
            // Create hierarchical states
            groundedState = new GroundedState();
            airborneState = new AirborneState();
            abilityState = new AbilityState();
            
            // Register with state machine
            stateMachine.AddState(groundedState);
            stateMachine.AddState(airborneState);
            stateMachine.AddState(abilityState);
            
            // Setup main state transitions
            SetupMainTransitions();
            
            // Set initial state
            stateMachine.SetInitialState(groundedState);
        }
        
        private void SetupMainTransitions()
        {
            // Grounded → Airborne (when not grounded)
            groundedState
                .When(() => !isGrounded && !IsInAbilityState(), airborneState, priority: 10, name: "Not Grounded");
            
            // Grounded → Ability (when ability pressed)
            groundedState
                .When(() => primaryAbilityPressed, abilityState, priority: 20, name: "Ability Pressed");
            
            // Airborne → Grounded (when grounded and falling)
            airborneState
                .When(() => isGrounded && rb.linearVelocity.y <= 0.1f, groundedState, priority: 10, name: "Landed");
            
            // Ability → Grounded (when finished and grounded)
            abilityState
                .When(() => IsAbilityFinished() && isGrounded, groundedState, priority: 10, name: "Finished (Grounded)");
            
            // Ability → Airborne (when finished and not grounded)
            abilityState
                .When(() => IsAbilityFinished() && !isGrounded, airborneState, priority: 10, name: "Finished (Airborne)");
        }
        
        #endregion
        
        #region Ground Detection
        
        private void CheckGrounded()
        {
            Vector3 origin = transform.position + groundCheckOffset;
            isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundLayer);
        }
        
        private void OnDrawGizmosSelected()
        {
            // Visualize ground check
            Vector3 origin = transform.position + groundCheckOffset;
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(origin, origin + Vector3.down * groundCheckDistance);
            Gizmos.DrawWireSphere(origin + Vector3.down * groundCheckDistance, 0.1f);
        }
        
        #endregion
        
        #region State Helpers
        
        private void HandleMainStateTransitions()
        {
            // This runs in addition to automatic transitions
            // You can add manual state switching logic here if needed
        }
        
        private bool IsInAbilityState()
        {
            return stateMachine.IsInState(abilityState);
        }
        
        private bool IsAbilityFinished()
        {
            return primaryAbilityFinished || secondaryAbilityFinished;
        }
        
        #endregion
        
        #region Ability Tracking
        
        public bool IsPrimaryAbilityFinished() => primaryAbilityFinished;
        public bool IsSecondaryAbilityFinished() => secondaryAbilityFinished;
        
        public void MarkPrimaryAbilityFinished()
        {
            primaryAbilityFinished = true;
            primaryAbilityPressed = false;
        }
        
        public void MarkSecondaryAbilityFinished()
        {
            secondaryAbilityFinished = true;
            secondaryAbilityPressed = false;
        }
        
        private void ResetAbilityFlags()
        {
            primaryAbilityFinished = false;
            secondaryAbilityFinished = false;
        }
        
        #endregion
        
        #region Input Methods (Call from Input System)
        
        public void OnMove(Vector2 input)
        {
            moveInput = input;
        }
        
        public void OnJump(bool pressed)
        {
            jumpPressed = pressed;
        }
        
        public void OnSprint(bool pressed)
        {
            sprintPressed = pressed;
        }
        
        public void OnPrimaryAbility(bool pressed)
        {
            if (pressed)
            {
                primaryAbilityPressed = true;
                primaryAbilityFinished = false;
            }
        }
        
        public void OnSecondaryAbility(bool pressed)
        {
            if (pressed)
            {
                secondaryAbilityPressed = true;
                secondaryAbilityFinished = false;
            }
        }
        
        #endregion
        
        #region Debug
        
        [ContextMenu("Force Grounded")]
        private void ForceGrounded() => stateMachine.TransitionTo(groundedState);
        
        [ContextMenu("Force Airborne")]
        private void ForceAirborne() => stateMachine.TransitionTo(airborneState);
        
        [ContextMenu("Force Ability")]
        private void ForceAbility() => stateMachine.TransitionTo(abilityState);
        
        [ContextMenu("Log Current State")]
        private void LogCurrentState()
        {
            Debug.Log($"Current State: {GetStateHierarchy()}");
        }
        
        #endregion
    }
}