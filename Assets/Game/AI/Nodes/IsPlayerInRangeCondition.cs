using System;
using Unity.Behavior;
using UnityEngine;

namespace Game.AI.Nodes{
    /// <summary>
    /// Unity Behavior condition: checks if the player is within a specified range of the companion.
    /// </summary>
    [Serializable]
    [Condition(
        name: "Is Player In Range",
        story: "Is [Player] within [Range] of [Self]",
        category: "Companion/Conditions")]
    public class IsPlayerInRangeCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<Transform> Player;
        [SerializeReference] public BlackboardVariable<Transform> Self;
        [SerializeReference] public BlackboardVariable<float> Range = new(10f);

        public override bool IsTrue()
        {
            if (Player.Value == null || Self.Value == null) return false;
            float dist = Vector3.Distance(Self.Value.position, Player.Value.position);
            return dist <= Range.Value;
        }
    }
}