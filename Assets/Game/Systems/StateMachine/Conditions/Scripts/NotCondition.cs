using loophouse.ScriptableStates;
using Obvious.Soap;
using UnityEngine;

namespace Game.Systems.StateMachine.Conditions.Scripts{
    [CreateAssetMenu(menuName = "Scriptable State Machine/Conditions/NotCondition", fileName = "new NotCondition")]
    public class NotCondition : ScriptableCondition{
        [SerializeField] private ScriptableCondition condition;
        [SerializeField] private BoolVariable isTrue;

        public override bool Verify(StateComponent statesComponent){
            if (condition == null)
            {
                Debug.LogWarning("Condition is null in NotCondition.");
                isTrue.Value = false;
                return false; // If condition is null, return false
            }

            // Verify the condition and invert the result
            bool result = condition.Verify(statesComponent);
            isTrue.Value = !result;
            return !result; // Return the negated result of the condition
        }
    }
}