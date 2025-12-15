using UnityEngine;

namespace Game.Player.States.Grounded{
    public class WalkState : PlayerState
    {
        private float walkSpeed = 3f;
        
        public WalkState() : base("Walk") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            Animator?.Play("Walk");
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            Vector3 moveDir = GetMoveDirection();
            if (Rigidbody != null && moveDir != Vector3.zero)
            {
                Vector3 targetVel = moveDir * walkSpeed;
                targetVel.y = Rigidbody.linearVelocity.y;
                Rigidbody.linearVelocity = Vector3.Lerp(Rigidbody.linearVelocity, targetVel, Time.deltaTime * 10f);
                
                // Rotate towards movement
                Transform.forward = Vector3.Slerp(Transform.forward, moveDir, Time.deltaTime * 10f);
            }
        }
    }
}