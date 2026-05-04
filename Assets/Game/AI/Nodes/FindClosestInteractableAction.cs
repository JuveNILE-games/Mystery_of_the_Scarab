using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes{
    /// <summary>
    /// Unity Behavior action: finds the closest Interactable within a search radius and outputs it
    /// along with its position for navigation.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Find Closest Interactable",
        story: "Find closest interactable within [SearchRadius] of [Self]",
        category: "Companion/Actions",
        id: "companion_find_closest_interactable")]
    public class FindClosestInteractableAction : Action
    {
        [SerializeReference] public BlackboardVariable<Transform> Self;
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new(12f);

        // Outputs
        [SerializeReference] public BlackboardVariable<GameObject> TargetInteractable;
        [SerializeReference] public BlackboardVariable<Vector3> TargetPosition;

        protected override Status OnStart()
        {
            if (Self.Value == null) return Status.Failure;

            Interactable best = null;
            float bestDist = float.MaxValue;

            foreach (var interactable in Interactable.All)
            {
                if (interactable == null) continue;
                float dist = Vector3.Distance(Self.Value.position, interactable.transform.position);
                if (dist <= SearchRadius.Value && dist < bestDist)
                {
                    bestDist = dist;
                    best = interactable;
                }
            }

            if (best != null)
            {
                TargetInteractable.Value = best.gameObject;
                TargetPosition.Value = best.transform.position;
                return Status.Success;
            }

            return Status.Failure;
        }
    }
}