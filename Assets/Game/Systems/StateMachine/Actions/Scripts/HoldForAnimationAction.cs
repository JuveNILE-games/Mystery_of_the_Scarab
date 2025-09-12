using Game.Player;
using loophouse.ScriptableStates;
using SpriteAnimations;
using UnityEngine;

namespace Game.Systems.StateMachine.Actions.Scripts{
    [CreateAssetMenu(menuName = "Scriptable State Machine/Actions/HoldForAnimation", fileName = "new HoldForAnimationAction")]
    public class HoldForAnimationAction : ScriptableAction
    {
        public override void Act(StateComponent statesComponent){
            if (statesComponent.TryGetComponent(out SpriteAnimator animator))
            {
                //get condition of current state transition
                if (statesComponent.TryGetComponent(out PlayerController controller))
                {
                    var stateTracker = controller.stateTracker.dictionary;
                    if (stateTracker.TryGetValue(statesComponent.CurrentState, out  ScriptableObject condition))
                    {
                        if (condition is Conditions.Scripts.InputCondition inputCondition)
                        {
                            inputCondition.isTrue.Value = !animator.IsPlaying;
                        }
                    }
                }
                
            }
            else
            {
                Debug.LogWarning($"SpriteAnimator component not found on {statesComponent.name}. Cannot hold for animation.");
            }
        }
    }
}
