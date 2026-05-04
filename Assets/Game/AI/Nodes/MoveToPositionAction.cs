using System;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes{
    /// <summary>
    /// Unity Behavior action: moves the NavMeshAgent to a target position and completes when within stop distance.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Move To Position",
        story: "Move [Agent] to [TargetPosition]",
        category: "Companion/Actions",
        id: "companion_move_to_position")]
    public class MoveToPositionAction : Action
    {
        [SerializeReference] public BlackboardVariable<NavMeshAgent> Agent;
        [SerializeReference] public BlackboardVariable<Vector3> TargetPosition;
        [SerializeReference] public BlackboardVariable<float> StopDistance = new(1.2f);

        protected override Status OnStart()
        {
            if (Agent.Value == null) return Status.Failure;
            Agent.Value.isStopped = false;
            Agent.Value.SetDestination(TargetPosition.Value);
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null) return Status.Failure;
            if (Agent.Value.pathPending) return Status.Running;
            if (Agent.Value.pathStatus == NavMeshPathStatus.PathInvalid) return Status.Failure;

            float dist = Vector3.Distance(Agent.Value.transform.position, TargetPosition.Value);
            if (dist <= StopDistance.Value)
            {
                Agent.Value.ResetPath();
                return Status.Success;
            }
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (Agent.Value != null && Agent.Value.hasPath)
            {
                Agent.Value.ResetPath();
            }
        }
    }
}