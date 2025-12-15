using UnityEngine;

namespace Game.Player.States.Grounded{
    public class JumpState : PlayerState
    {
        private float jumpForce = 5f;
        
        public JumpState() : base("Jump") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            
            if (Rigidbody != null)
            {
                Vector3 vel = Rigidbody.linearVelocity;
                vel.y = jumpForce;
                Rigidbody.linearVelocity = vel;
            }
            
            Animator?.Play("Jump");
        }
    }
}