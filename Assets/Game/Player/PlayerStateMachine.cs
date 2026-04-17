using Core.Systems.Bindables;
using Core.Systems.StateMachine;
using Core.Definitions.Character;
using Game.Player.States;
using Game.Player.States.Ability;
using Game.Player.States.Grounded;
using SpriteAnimations;
using UnityEngine;
using Game.AI;

namespace Game.Player
{
    /// <summary>
    /// Player state machine that manages all player states and transitions.
    /// Hierarchical structure:
    /// - Grounded (Idle, Walk, Sprint, Jump, Land)
    /// - Airborne (Falling)
    /// - Ability (PrimaryAbility, SecondaryAbility)
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerStateMachine : StateMachineComponent<PlayerStateMachine, BaseState<PlayerStateMachine, PlayerState>>, IMovementControllable
    {
        [Header("Component References")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private SpriteAnimator anim;
        
        [Header("Data")]
        [SerializeField] private CharacterData initialCharacterData;
        
        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float groundCheckDistance = 0.2f;
        
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
        
        public CharacterController Controller => controller;
        public SpriteAnimator Animator => anim;
        public Vector2 MoveInput => moveInput;
        public bool IsGrounded => isGrounded;
        public bool IsJumpPressed => jumpPressed;
        public bool IsSprintPressed => sprintPressed;
        public bool IsPrimaryAbilityPressed => primaryAbilityPressed;
        public bool IsSecondaryAbilityPressed => secondaryAbilityPressed;
        
        public Bindable<CharacterData> Data { get; private set; } = new();
        
        public Vector2 LastMoveDirection { get; private set; } = Vector2.down;
        
        #endregion
        
        #region Unity Lifecycle
        
        protected override void Awake()
        {
            // Cache components
            if (controller == null) controller = GetComponent<CharacterController>();
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
            
            if (initialCharacterData != null)
            {
                Data.Value = initialCharacterData;
            }
            
            base.Awake();
        }
        
        private void OnValidate()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
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
                .When(() => isGrounded && controller.velocity.y <= 0.1f, groundedState, priority: 10, name: "Landed");
            
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
            if (controller == null) return;
            
            // SphereCast Logic:
            // We want to cast a sphere from the bottom of the controller downwards.
            // Problem: SphereCast won't detect colliders it starts INSIDE. CharacterController often sinks slightly (SkinWidth).
            // Fix: Start the cast slightly HIGHER (retreat up) and increase the distance to compensate.
            
            float radius = controller.radius * 0.9f;
            float castRetreat = 0.1f; // Move start point up by this much
            
            // Calculate standard bottom sphere center
            Vector3 centerOffset = Vector3.down * (controller.height * 0.5f - radius);
            Vector3 bottomCenter = transform.position + controller.center + centerOffset;
            
            // Move origin UP
            Vector3 origin = bottomCenter + Vector3.up * castRetreat;
            
            // Increase max distance
            float actualDistance = groundCheckDistance + castRetreat;
            
            // SphereCast
            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, actualDistance, groundLayer))
            {
                // Validate Slope
                float angle = Vector3.Angle(Vector3.up, hit.normal);
                if (angle <= controller.slopeLimit) // Default is usually 45
                {
                    isGrounded = true;
                }
                else
                {
                    // Too steep! Treat as wall/air.
                    // Debug.Log($"[PlayerSM] Too steep: {angle} deg on {hit.collider.name}");
                    isGrounded = false;
                }
            }
            else
            {
                isGrounded = false;
            }
            
            // Debugging collision with Controller fallback (Be careful with this on walls)
            if (!isGrounded && controller.isGrounded)
            {
                 // Controller says grounded. Trust it? 
                 // It might also be sliding down a wall if slope limit isn't configured right on the CC.
                 // Let's trust it but only if velocity is low? No, safer to just trust it for now.
                 isGrounded = true; 
            }
        }
        
        private void OnDrawGizmosSelected()
        {
             if (controller == null) controller = GetComponent<CharacterController>();
             if (controller == null) return;

             float radius = controller.radius * 0.9f;
             float castRetreat = 0.1f;
             
             Vector3 centerOffset = Vector3.down * (controller.height * 0.5f - radius);
             Vector3 bottomCenter = transform.position + controller.center + centerOffset;
             Vector3 origin = bottomCenter + Vector3.up * castRetreat;
             float actualDistance = groundCheckDistance + castRetreat;
             
             Gizmos.color = isGrounded ? Color.green : Color.red;
             // Draw start
             Gizmos.DrawWireSphere(origin, radius); 
             // Draw end
             Gizmos.DrawWireSphere(origin + Vector3.down * actualDistance, radius);
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
            // Debug.Log($"[PlayerSM] OnMove: {input}");
            moveInput = input;
            if (moveInput.sqrMagnitude > 0.01f)
            {
                LastMoveDirection = GetCardinalDirection(moveInput);
            }
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

        public Vector2 GetCardinalDirection(Vector2 input)
        {
            if (input == Vector2.zero) return Vector2.down;

            // Determine dominant axis
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            {
                // Horizontal is dominant
                return input.x > 0 ? Vector2.right : Vector2.left;
            }
            else
            {
                // Vertical is dominant
                return input.y > 0 ? Vector2.up : Vector2.down;
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