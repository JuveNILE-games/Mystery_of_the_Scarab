using Core.Systems.StateMachine.Components;
using Core.Systems.StateMachine.Core;
using Core.Systems.StateMachine.Core.Conditions;
using Game.Player.States;
using Game.Player.States.Airborne;
using Game.Player.States.Grounded;
using UnityEngine;

namespace Game.Player
{
    [RequireComponent(typeof(StateMachineDriver))]
    public class PlayerDriver : MonoBehaviour, IStateMachineComponent
    {
        private StateMachine _stateMachine;
        private StateMachineDriver _stateMachineDriver;
        
        [Header("References")]
        public Rigidbody2D rigidbody2D;
        public Animator animator;
        
        [Header("Input")]
        public Core.Systems.InputManagement.InputManager inputManager;
        
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float jumpForce = 10f;
        public LayerMask groundLayer;
        public Transform groundCheck;
        public float groundCheckRadius = 0.2f;
        
        private void Awake()
        {
            _stateMachineDriver = GetComponent<StateMachineDriver>();
            _stateMachine = new StateMachine(this);
            _stateMachineDriver.Initialize(_stateMachine);
        }
        
        private void Start()
        {
            // Initialize the state hierarchy
            InitializeStateHierarchy();
        }
        
        private void InitializeStateHierarchy()
        {
            // Create states
            var playerState = ScriptableObject.CreateInstance<PlayerState>();
            var groundedState = ScriptableObject.CreateInstance<GroundedState>();
            var idleState = ScriptableObject.CreateInstance<IdleState>();
            var walkState = ScriptableObject.CreateInstance<WalkState>();
            var sprintState = ScriptableObject.CreateInstance<SprintState>();
            var airborneState = ScriptableObject.CreateInstance<AirborneState>();
            var jumpState = ScriptableObject.CreateInstance<JumpState>();
            var fallState = ScriptableObject.CreateInstance<FallState>();
            
            // Set up parent-child relationships
            groundedState.Parent = playerState;
            idleState.Parent = groundedState;
            walkState.Parent = groundedState;
            sprintState.Parent = groundedState;
            
            airborneState.Parent = playerState;
            jumpState.Parent = airborneState;
            fallState.Parent = airborneState;
            
            // Set up transitions
            // From idle to walk
            idleState.transitions.Add(new StateTransition(walkState, 
                new PredicateCondition(() => GetMovementDirection().magnitude > 0.1f)));
            
            // From walk to idle
            walkState.transitions.Add(new StateTransition(idleState, 
                new PredicateCondition(() => GetMovementDirection().magnitude <= 0.1f)));
            
            // From grounded to airborne
            groundedState.transitions.Add(new StateTransition(airborneState, 
                new PredicateCondition(() => !IsGrounded())));
            
            // From airborne to grounded
            airborneState.transitions.Add(new StateTransition(groundedState, 
                new PredicateCondition(() => IsGrounded())));
            
            // Initialize state machine
            _stateMachine.Initialize(playerState);
        }
        
        #region IStateMachineComponent Implementation
        
        public Vector2 GetMovementDirection()
        {
            return inputManager.movementDirection;
        }
        
        public void SetMovement(Vector2 direction)
        {
            if (rigidbody2D != null)
            {
                rigidbody2D.linearVelocity = new Vector2(direction.x * moveSpeed, rigidbody2D.linearVelocity.y);
            }
        }
        
        public void SetVelocity(Vector2 velocity)
        {
            if (rigidbody2D != null)
            {
                rigidbody2D.linearVelocity = velocity;
            }
        }
        
        public bool IsGrounded()
        {
            if (groundCheck == null) return false;
            return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        }
        
        public void PlayAnimation(string animationName)
        {
            if (animator != null)
            {
                animator.Play(animationName);
            }
        }
        
        public bool IsAnimationPlaying(string animationName)
        {
            if (animator != null)
            {
                return animator.GetCurrentAnimatorStateInfo(0).IsName(animationName);
            }
            return false;
        }
        
        public bool CanUsePrimaryAbility()
        {
            // Implementation depends on your ability system
            return true;
        }
        
        public bool CanUseSecondaryAbility()
        {
            // Implementation depends on your ability system
            return true;
        }
        
        public void UsePrimaryAbility()
        {
            // Implementation depends on your ability system
        }
        
        public void UseSecondaryAbility()
        {
            // Implementation depends on your ability system
        }
        
        public bool IsJumpPressed()
        {
            // This would need to be implemented based on your input system
            return inputManager != null; // Placeholder
        }
        
        public bool IsSprintPressed()
        {
            return inputManager != null && inputManager.movementDirection.magnitude > 0.1f; // Placeholder
        }
        
        public bool IsPrimaryAbilityPressed()
        {
            return false; // Placeholder
        }
        
        public bool IsSecondaryAbilityPressed()
        {
            return false; // Placeholder
        }
        
        #endregion
    }
}