using UnityEngine;

namespace Game.Player.States.Ability{
    public class PrimaryAbilityState : PlayerState{
        private float abilityDuration = 0.5f;
        private float startTime;

        public PrimaryAbilityState() : base("PrimaryAbility"){
        }

        public override void OnEnter(){
            base.OnEnter();
            startTime = Time.time;

            Animator?.Play("PrimaryAbility");
            Debug.Log("[PlayerSM] Executing Primary Ability");

            // Execute ability logic here
            ExecutePrimaryAbility();
        }

        public override void OnUpdate(){
            base.OnUpdate();

            // Mark as finished after duration
            if (Time.time - startTime >= abilityDuration)
            {
                Owner?.MarkPrimaryAbilityFinished();
            }
        }

        private void ExecutePrimaryAbility(){
            // Example: Apply forward force
            if (Rigidbody != null)
            {
                Vector3 dashDir = Transform.forward;
                Rigidbody.AddForce(dashDir * 10f, ForceMode.Impulse);
            }
        }
    }
}