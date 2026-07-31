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

            if (Animator != null) Animator.Play("PrimaryAbility");
            Debug.Log("[PlayerSM] Executing Primary Ability");

            // Execute ability logic here
            ExecutePrimaryAbility();
        }

        public override void OnUpdate(){
            base.OnUpdate();

            // Mark as finished after duration
            if (Time.time - startTime >= abilityDuration)
            {
                if (Owner != null) Owner.MarkPrimaryAbilityFinished();
            }
            else
            {
                if (Owner != null && Transform != null)
                {
                    Owner.AdditionalVelocity = Transform.forward * 20f;
                }
            }
        }

        private void ExecutePrimaryAbility(){
            // Example: Apply forward force
            // Example: Move forward manually
             if (Controller != null)
             {
                 // This is just a one-frame push, which might be subtle.
                 // Ideally, we'd apply velocity over the duration in Update.
                 // For now, let's just log it.
                 Debug.Log("Dash!");
             }
        }
    }
}
