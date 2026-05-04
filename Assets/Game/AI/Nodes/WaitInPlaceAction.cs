using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes
{
    [Serializable]
    [NodeDescription(
        name: "Wait In Place",
        story: "[Agent] stands still and waits",
        category: "Action/Companion",
        id: "companion_wait_in_place")]
    public class WaitInPlaceAction : Action
    {
        [SerializeReference] public BlackboardVariable<UnityEngine.AI.NavMeshAgent> Agent;

        protected override Status OnStart()
        {
            if (Agent.Value != null)
            {
                Agent.Value.isStopped = true;
                Agent.Value.velocity = Vector3.zero;
            }
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (Agent.Value != null)
                Agent.Value.isStopped = false;
        }
    }
}
