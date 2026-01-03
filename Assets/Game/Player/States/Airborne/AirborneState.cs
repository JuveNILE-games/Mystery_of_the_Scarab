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
        private Vector3 currentVelocity;

        public override void OnEnter()
        {
            base.OnEnter();
            Debug.Log("[PlayerSM] Entered Airborne state");
            
            // Inherit velocity from controller (if any) or start fresh 
            currentVelocity = Owner.Controller != null ? Owner.Controller.velocity : Vector3.zero;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // 1. Apply Gravity
            ApplyGravity();
            
            // 2. Apply Air Control (modify horizontal velocity)
            ApplyAirControl();
            
            // 3. Move Controller
            if (Owner.Controller != null)
            {
                Owner.Controller.Move(currentVelocity * Time.deltaTime);
            }
        }

        private void ApplyGravity()
        {
            if (Owner.Data.Value == null) return;
            
            float gravityMult = Owner.Data.Value.GravityMultiplier;
            float maxFall = Owner.Data.Value.MaxFallSpeed;
            
            // Standard gravity
            float gravity = Physics.gravity.y * (gravityMult > 0 ? gravityMult : 1f);
            
            currentVelocity.y += gravity * Time.deltaTime;
            
            // Clamp fall speed
            if (currentVelocity.y < -maxFall)
            {
                currentVelocity.y = -maxFall;
            }
        }
        
        private void ApplyAirControl()
        {
            if (Owner == null) return;
            
            Vector3 moveDir = GetMoveDirection();
            if (moveDir != Vector3.zero)
            {
                float airControlForce = Owner.Data.Value != null ? Owner.Data.Value.AirControl : 2f;
                
                // Add horizontal acceleration
                // Note: With CharacterController, we manipulate velocity directly.
                // We'll treat airControlForce as an acceleration here.
                Vector3 targetAccel = moveDir * airControlForce * Time.deltaTime; // Simple accumulation
                
                currentVelocity.x += targetAccel.x;
                currentVelocity.z += targetAccel.z;
                
                // Clamp horizontal speed
                Vector3 horizontalVel = new Vector3(currentVelocity.x, 0, currentVelocity.z);
                if (horizontalVel.magnitude > 6f)
                {
                    horizontalVel = horizontalVel.normalized * 6f;
                    currentVelocity.x = horizontalVel.x;
                    currentVelocity.z = horizontalVel.z;
                }
            }
        }

        private Vector3 GetMoveDirection()
        {
            if (Owner == null) return Vector3.zero;
            
            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : Owner.transform.forward;
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Owner.transform.right;
            
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
            
            return (forward * Owner.MoveInput.y + right * Owner.MoveInput.x).normalized;
        }
    }
