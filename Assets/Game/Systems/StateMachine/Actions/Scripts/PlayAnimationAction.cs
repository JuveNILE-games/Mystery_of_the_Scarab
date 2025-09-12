using Core.Systems.InputManagement;
using Game.Player;
using loophouse.ScriptableStates;
using SpriteAnimations;
using UnityEngine;

namespace Game.Systems.StateMachine.Actions.Scripts{
    [CreateAssetMenu(menuName = "Scriptable State Machine/Actions/PlayAnimation", fileName = "new PlayAnimationAction")]
    public class PlayAnimationAction : ScriptableAction
    {
        private SpriteAnimation _animation;
        public override void Act(StateComponent statesComponent){
            if (statesComponent.TryGetComponent(out SpriteAnimator animator))
            {
                //find the animation corresponding to the state
                _animation = animator.AnimationsList.Find(animation => animation.name == statesComponent.CurrentState.name);
                // if the animation is of type WindroseAnimation play it with the player movement direction
                if (_animation is SpriteAnimationWindrose windroseAnimation)
                {
                    Vector2 direction = statesComponent.GetComponent<PlayerInputInitializer>().PlayerInputManager.movementDirection;
                    animator.Play<WindroseAnimator>(windroseAnimation.name).SetDirection(direction);
                }
                else
                {
                    animator.Play(_animation);
                }
            }
            else
            {
                Debug.LogWarning($"SpriteAnimator component not found on {statesComponent.name}. Animation {_animation.name} cannot be played.");
            }
        }
    }
}
