using System;
using Unity.Behavior;
using UnityEngine;

namespace Game.AI.Nodes
{
    [Serializable]
    [Condition(
        name: "Is Holding",
        story: "[Self] is currently holding a position",
        category: "Condition/Companion",
        id: "companion_is_holding")]
    public class IsHoldingCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<bool> IsHolding = new();
        public override bool IsTrue() => IsHolding != null && IsHolding.Value;
    }
}
