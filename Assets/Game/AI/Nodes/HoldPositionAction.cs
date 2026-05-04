using System;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes
{
    [Serializable]
    [NodeDescription(
        name: "Hold Position",
        story: "[Agent] holds position at [HoldTarget]",
        category: "Companion/Actions",
        id: "companion_hold_position")]
    public class HoldPositionAction : Action
    {
        [SerializeReference] public BlackboardVariable<NavMeshAgent> Agent;
        [SerializeReference] public BlackboardVariable<GameObject> HoldTarget;

        // Exits when: the target component becomes met (puzzle solved),
        // OR the puzzle is solved (no longer needed),
        // OR a new target appears.
        [SerializeReference] public BlackboardVariable<bool> ShouldRelease;

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;
            Agent.Value.isStopped = true;
            Agent.Value.ResetPath();
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (ShouldRelease.Value) return Status.Success;

            // Drift correction: if we've been nudged off position, walk back.
            if (HoldTarget.Value != null)
            {
                float drift = Vector3.Distance(
                    Agent.Value.transform.position,
                    HoldTarget.Value.transform.position);
                if (drift > 0.3f)
                {
                    Agent.Value.isStopped = false;
                    Agent.Value.SetDestination(HoldTarget.Value.transform.position);
                }
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (Agent.Value != null) Agent.Value.isStopped = false;
        }
    }
}
