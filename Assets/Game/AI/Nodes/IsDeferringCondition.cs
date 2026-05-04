using System;
using Unity.Behavior;
using UnityEngine;

namespace Game.AI.Nodes
{
    [Serializable]
    [Condition(
        name: "Is Deferring",
        story: "Is deference timer [DeferenceTimer] > 0",
        category: "Companion/Conditions",
        id: "companion_is_deferring")]
    public class IsDeferringCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<float> DeferenceTimer = new();
        public override bool IsTrue() => DeferenceTimer != null && DeferenceTimer.Value > 0f;
    }
}
