using Core.Systems.Bindables;
using Core.Systems.StateMachine;
using Core.Definitions.Character;
using Game.Player.States;
using Game.Player.States.Ability;
using Game.Player.States.Grounded;
using SpriteAnimations;
using UnityEngine;
using Game.AI;
using System.Collections.Generic;

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
        private static readonly List<PlayerStateMachine> ActivePlayers = new();

        [Header("Component References")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private SpriteAnimator anim;
        
        [Header("Data")]
        [SerializeField] private CharacterData initialCharacterData;
        
        [Header("Ground Detection")]
        [SerializeField] private LayerMask groundLayer;
        // 0.2f was too shallow for a dynamic NavMesh: micro-gaps between bakes caused 1-2 frame
        // airborne flickers, switching the companion into air-control movement and killing speed.
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private float ungroundedGraceTime = 0.08f;
        [SerializeField] private float coyoteTimeDuration = 0.12f;
        
        [Header("Input (Connect from InputSystem)")]
        // World-space XZ movement direction (magnitude 0-1). Set by PlayerInputInitializer
        // (after camera projection) or AIMovementBridge (NavMesh desiredVelocity.xz directly).
        // Never set from camera-relative 2D inside a state.
        [SerializeField] private Vector3 worldMoveInput;
        [SerializeField] private bool jumpPressed;
        [SerializeField] private bool sprintPressed;
        [SerializeField] private bool primaryAbilityPressed;
        [SerializeField] private bool secondaryAbilityPressed;

        [Header("Runtime Motion")]
        [SerializeField] private Vector3 additionalVelocity;
        
        // State management
        private GroundedState groundedState;
        private AirborneState airborneState;
        private AbilityState abilityState;
        
        // Ability tracking
        private bool primaryAbilityFinished;
        private bool secondaryAbilityFinished;
        
        // Grounded check cache
        private bool isGrounded;
        private bool hasValidGroundContact;
        private float lastGroundedTime = float.NegativeInfinity;
        private float lastValidGroundContactTime = float.NegativeInfinity;
        private bool coyoteJumpConsumed;
        private Collider[] passthroughColliders;
        
        #region Public Properties (accessed by states)
        
        public CharacterController Controller => controller;
        public SpriteAnimator Animator => anim;

        /// <summary>World-space XZ movement direction (magnitude 0–1). Y is always 0.</summary>
        public Vector3 WorldMoveInput => worldMoveInput;

        /// <summary>
        /// Convenience XZ projection of WorldMoveInput as Vector2 (x→x, z→y).
        /// Used by GroundedState.HasInput() checks and cardinal direction animation mapping.
        /// </summary>
        public Vector2 MoveInput => new Vector2(worldMoveInput.x, worldMoveInput.z);

        public bool IsGrounded => isGrounded;
        public bool CanUseCoyoteJump =>
            !hasValidGroundContact &&
            !coyoteJumpConsumed &&
            (Time.time - lastValidGroundContactTime) <= coyoteTimeDuration;

        public bool IsJumpPressed => jumpPressed;
        public bool IsSprintPressed => sprintPressed;
        public bool IsPrimaryAbilityPressed => primaryAbilityPressed;
        public bool IsSecondaryAbilityPressed => secondaryAbilityPressed;
        
        public Bindable<CharacterData> Data { get; private set; } = new();
        public float VerticalVelocity { get; set; }
        public Vector3 HorizontalVelocity { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public Vector3 AdditionalVelocity
        {
            get => additionalVelocity;
            set => additionalVelocity = value;
        }
        
        /// <summary>Last non-zero cardinal direction for sprite animation on state entry.</summary>
        public Vector2 LastMoveDirection { get; private set; } = Vector2.down;
        
        #endregion
        
        #region Unity Lifecycle
        
        protected override void Awake()
        {
            // Cache components
            if (controller == null) controller = GetComponent<CharacterController>();
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
            passthroughColliders = GetComponents<Collider>();
            RegisterPlayerPassthrough();
            
            if (initialCharacterData != null)
            {
                Data.Value = initialCharacterData;
            }
            
            base.Awake();
        }

        private void OnDestroy()
        {
            UnregisterPlayerPassthrough();
        }
        
        private void OnValidate()
        {
            if (controller == null) controller = GetComponent<CharacterController>();
            if (anim == null) anim = GetComponentInChildren<SpriteAnimator>();
        }
        
        protected override void Update()
        {
            CheckGrounded();
            HandleMainStateTransitions();
            base.Update();
            ApplyMovement();
        }
        
        #endregion

        private void RegisterPlayerPassthrough()
        {
            for (int i = 0; i < ActivePlayers.Count; i++)
            {
                SetPlayerPassthrough(this, ActivePlayers[i], true);
            }

            ActivePlayers.Add(this);
        }

        private void UnregisterPlayerPassthrough()
        {
            ActivePlayers.Remove(this);

            for (int i = 0; i < ActivePlayers.Count; i++)
            {
                SetPlayerPassthrough(this, ActivePlayers[i], false);
            }
        }

        private static void SetPlayerPassthrough(PlayerStateMachine a, PlayerStateMachine b, bool ignore)
        {
            if (a == null || b == null || a.passthroughColliders == null || b.passthroughColliders == null)
            {
                return;
            }

            for (int i = 0; i < a.passthroughColliders.Length; i++)
            {
                Collider colliderA = a.passthroughColliders[i];
                if (colliderA == null)
                {
                    continue;
                }

                for (int j = 0; j < b.passthroughColliders.Length; j++)
                {
                    Collider colliderB = b.passthroughColliders[j];
                    if (colliderB == null || colliderA == colliderB)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(colliderA, colliderB, ignore);
                }
            }
        }
        
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
            // Grounded → Airborne (jump started or no longer grounded)
            groundedState
                .When(() => (!isGrounded || VerticalVelocity > 0f) && !IsInAbilityState(), airborneState, priority: 10, name: "Not Grounded");
            
            // Grounded → Ability (when ability pressed)
            groundedState
                .When(() => primaryAbilityPressed, abilityState, priority: 20, name: "Ability Pressed");
            
            // Airborne → Grounded (when grounded and descending)
            airborneState
                .When(() => isGrounded && VerticalVelocity <= 0f, groundedState, priority: 10, name: "Landed");
            
            // Ability → Grounded (when finished and grounded)
            abilityState
                .When(() => IsAbilityFinished() && isGrounded, groundedState, priority: 10, name: "Finished (Grounded)");
            
            // Ability → Airborne (when finished and not grounded)
            abilityState
                .When(() => IsAbilityFinished() && !isGrounded, airborneState, priority: 10, name: "Finished (Airborne)");
        }
        
        #endregion
        
        #region Ground Detection

        public bool CanStartJump()
        {
            return hasValidGroundContact || CanUseCoyoteJump;
        }

        public void ConsumeJumpEligibility()
        {
            if (!hasValidGroundContact)
            {
                coyoteJumpConsumed = true;
            }
        }
        
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
            
            bool hasValidGroundHit = false;

            // SphereCast
            if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, actualDistance, groundLayer))
            {
                // Validate Slope
                float angle = Vector3.Angle(Vector3.up, hit.normal);
                if (angle <= controller.slopeLimit) // Default is usually 45
                {
                    hasValidGroundHit = true;
                }
            }
            
            // CharacterController can report grounded when standing on dynamic colliders (for example,
            // the companion). Only trust that fallback when the supporting collider is in groundLayer.
            if (!hasValidGroundHit && controller.isGrounded)
            {
                float supportProbeDistance = actualDistance + controller.skinWidth;
                if (Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit supportHit, supportProbeDistance, groundLayer, QueryTriggerInteraction.Ignore))
                {
                    float supportAngle = Vector3.Angle(Vector3.up, supportHit.normal);
                    if (supportAngle <= controller.slopeLimit)
                    {
                        hasValidGroundHit = true;
                    }
                }
            }

            if (hasValidGroundHit)
            {
                hasValidGroundContact = true;
                isGrounded = true;
                lastGroundedTime = Time.time;
                lastValidGroundContactTime = Time.time;
                coyoteJumpConsumed = false;
                return;
            }

            hasValidGroundContact = false;
            bool inUngroundedGraceWindow = (Time.time - lastGroundedTime) <= ungroundedGraceTime;
            isGrounded = inUngroundedGraceWindow && VerticalVelocity <= 0f;
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

        private void ApplyMovement()
        {
            if (controller == null)
            {
                additionalVelocity = Vector3.zero;
                return;
            }

            CharacterData data = Data.Value;
            float gravity = data != null ? data.Gravity : Physics.gravity.y * 2f;
            float groundStickForce = data != null ? data.GroundStickForce : -5f;
            float moveSpeed = data != null ? data.MoveSpeed : 3f;

            if (!isGrounded)
            {
                VerticalVelocity += gravity * Time.deltaTime;
            }
            else if (VerticalVelocity < 0f)
            {
                VerticalVelocity = groundStickForce;
            }

            HorizontalVelocity = worldMoveInput * moveSpeed * SpeedMultiplier;
            Vector3 velocity = HorizontalVelocity + (Vector3.up * VerticalVelocity) + additionalVelocity;
            controller.Move(velocity * Time.deltaTime);

            additionalVelocity = Vector3.zero;
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
        
        #region Input Methods

        /// <summary>
        /// Receive world-space movement direction. Called by PlayerInputInitializer (human) and
        /// AIMovementBridge (AI). Camera projection must already be applied by the caller.
        /// </summary>
        public void OnMoveWorldSpace(Vector3 worldDir)
        {
            worldMoveInput = new Vector3(worldDir.x, 0f, worldDir.z); // enforce Y=0
            if (worldMoveInput.sqrMagnitude > 0.01f)
            {
                // Derive cardinal direction from world XZ — correct for both human and AI
                // because human input has already been projected to world space by caller.
                LastMoveDirection = GetCardinalDirection(new Vector2(worldMoveInput.x, worldMoveInput.z));
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
                // West/East - Inverted to match animator
                return input.x > 0 ? Vector2.left : Vector2.right;
            }
            else
            {
                // North/South - Inverted to match animator
                return input.y > 0 ? Vector2.down : Vector2.up;
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
