using loophouse.ScriptableStates;
using Obvious.Soap;
using UnityEngine;

namespace Game.Systems.StateMachine.Conditions.Scripts{
    [CreateAssetMenu(menuName = "Scriptable State Machine/Conditions/MultiCondition", fileName = "new MultiCondition")]
    public class MultiCondition : ScriptableCondition
    {
        [SerializeField] private ScriptableCondition[] conditions;
        [SerializeField] private BoolVariable isTrue;
        public override bool Verify(StateComponent statesComponent){
            if (conditions == null || conditions.Length == 0)
            {
                Debug.LogWarning("No conditions defined for MultiCondition.");
                return false;
            }

            foreach (var condition in conditions)
            {
                if (condition == null)
                {
                    Debug.LogWarning("A condition is null in MultiCondition.");
                    continue;
                }

                if (!condition.Verify(statesComponent))
                {
                    isTrue.Value = false;
                    return false; // If any condition fails, return false
                }
            }
            isTrue.Value = true; // All conditions passed
            return true; // All conditions passed
        }
    }
}
