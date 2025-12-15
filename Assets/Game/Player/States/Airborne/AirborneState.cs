using Core.Systems.StateMachine;
using Game.Player;
using Game.Player.States;
using UnityEngine;

/// <summary>
    /// Hierarchical state for airborne movement.
    /// Contains: Falling
    /// </summary>
    public class AirborneState : HierarchicalState<PlayerStateMachine, PlayerState>
    {
        private FallingState fallingState;
        
        public AirborneState() : base("Airborne") { }
        
        protected override void Initialize()
        {
            fallingState = AddChildState(new FallingState());
            SetInitialChildState(fallingState);
        }
        
        public override void OnEnter()
        {
            base.OnEnter();
            Debug.Log("[PlayerSM] Entered Airborne state");
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Allow air control
            ApplyAirControl();
        }
        
        private void ApplyAirControl()
        {
            if (Owner == null || Rigidbody == null) return;
            
            Vector3 moveDir = GetMoveDirection();
            if (moveDir != Vector3.zero)
            {
                float airControlForce = 2f;
                Rigidbody.AddForce(moveDir * airControlForce, ForceMode.Force);
                
                // Clamp horizontal speed
                Vector3 vel = Rigidbody.linearVelocity;
                Vector3 horizontalVel = new Vector3(vel.x, 0, vel.z);
                if (horizontalVel.magnitude > 6f)
                {
                    horizontalVel = horizontalVel.normalized * 6f;
                    vel.x = horizontalVel.x;
                    vel.z = horizontalVel.z;
                    Rigidbody.linearVelocity = vel;
                }
            }
        }
        
        private bool HasInput() => Owner?.MoveInput.sqrMagnitude > 0.01f;
        
        protected Vector3 GetMoveDirection()
        {
            if (Owner == null) return Vector3.zero;
            
            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : Transform.forward;
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Transform.right;
            
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
            
            return (forward * Owner.MoveInput.y + right * Owner.MoveInput.x).normalized;
        }
        
        protected Transform Transform => Owner?.transform;
        protected Rigidbody Rigidbody => Owner?.Rigidbody;
    }