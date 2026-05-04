using System;
using Unity.Behavior;
using UnityEngine;

namespace Game.AI.Nodes
{
    [Serializable]
    [Condition(
        name: "Has Target",
        story: "[Self] has an actionable puzzle target",
        category: "Condition/Companion",
        id: "companion_has_target")]
    public class HasTargetCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<bool> HasTarget = new();
        public override bool IsTrue() => HasTarget != null && HasTarget.Value;
    }
}
