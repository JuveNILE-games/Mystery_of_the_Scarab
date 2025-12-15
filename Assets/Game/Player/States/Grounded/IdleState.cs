using UnityEngine;

namespace Game.Player.States.Grounded{
    public class IdleState : PlayerState
    {
        public IdleState() : base("Idle") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            Animator?.Play("Idle");
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Apply light damping
            if (Rigidbody != null)
            {
                Vector3 vel = Rigidbody.linearVelocity;
                vel.x *= 0.9f;
                vel.z *= 0.9f;
                Rigidbody.linearVelocity = vel;
            }
        }
    }
}