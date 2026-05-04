using Core.Systems.StateMachine;
using UnityEngine;

namespace Game.Player.States.Grounded
{
    /// <summary>
    /// Hierarchical state representing all grounded movement states.
    /// Contains: Idle, Walk, Sprint, Jump, Land
    /// </summary>
    public class GroundedState : HierarchicalState<PlayerStateMachine, PlayerState>
    {
        // Child states
        private IdleState idleState;
        private WalkState walkState;
        private SprintState sprintState;
        private JumpState jumpState;
        private LandState landState;
        
        public GroundedState() : base("Grounded") { }
        
        protected override void Initialize()
        {
            // Create and add child states
            idleState = AddChildState(new IdleState());
            walkState = AddChildState(new WalkState());
            sprintState = AddChildState(new SprintState());
            jumpState = AddChildState(new JumpState());
            landState = AddChildState(new LandState());
            
            // Set initial child state
            SetInitialChildState(idleState);
            
            // Setup transitions between child states
            SetupTransitions();
        }
        
        private void SetupTransitions()
        {
            // Idle transitions
            idleState
                .When(() => HasInput() && !Owner.IsSprintPressed, walkState, name: "Walking")
                .When(() => HasInput() && Owner.IsSprintPressed, sprintState, name: "Sprinting pressed and moving")
                .When(() => Owner.IsJumpPressed, jumpState, name: "Jump pressed");
            
            // Walk transitions
            walkState
                .When(() => !HasInput(), idleState, name: "No input")
                .When(() => Owner.IsSprintPressed, sprintState, name: "Sprint pressed")
                .When(() => Owner.IsJumpPressed, jumpState, name: "Jump pressed");
            
            // Sprint transitions
            sprintState
                .When(() => !HasInput(), idleState , name: "No input")
                .When(() => !Owner.IsSprintPressed, walkState , name: "Sprint released")
                .When(() => Owner.IsJumpPressed, jumpState , name: "Jump pressed");
            
            // Jump transitions
            jumpState
                .After(0.3f, idleState, priority: -1); // Fallback after jump
            
            // Land transitions
            landState
                .After(0.2f, idleState); // Quick transition back to idle
        }
        
        public override void OnEnter()
        {
            base.OnEnter();
            if (Owner != null && Owner.VerticalVelocity < 0f)
            {
                Owner.VerticalVelocity = Owner.Data.Value != null ? Owner.Data.Value.GroundStickForce : -5f;
            }
            Debug.Log("[PlayerSM] Entered Grounded state");
        }
        
        public override void OnExit()
        {
            Debug.Log("[PlayerSM] Exited Grounded state");
            base.OnExit();
        }
        
        private bool HasInput()
        {
            bool has = Owner?.MoveInput.sqrMagnitude > 0.01f;
            return has;
        }
    }
}
