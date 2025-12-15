using UnityEngine;

namespace Game.Player.States.Grounded{
    public class SprintState : PlayerState
    {
        private float sprintSpeed = 6f;
        
        public SprintState() : base("Sprint") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            Animator?.Play("Sprint");
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            Vector3 moveDir = GetMoveDirection();
            if (Rigidbody != null && moveDir != Vector3.zero)
            {
                Vector3 targetVel = moveDir * sprintSpeed;
                targetVel.y = Rigidbody.linearVelocity.y;
                Rigidbody.linearVelocity = Vector3.Lerp(Rigidbody.linearVelocity, targetVel, Time.deltaTime * 8f);
                
                // Rotate towards movement
                Transform.forward = Vector3.Slerp(Transform.forward, moveDir, Time.deltaTime * 12f);
            }
        }
    }
}