using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes
{
    [Serializable]
    [NodeDescription(
        name: "Find Puzzle Target",
        story: "Find best unmet puzzle component for [Self] in [SearchRadius]",
        category: "Companion/Actions/Puzzle",
        id: "companion_find_puzzle_target")]
    public class FindPuzzleTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<Transform> Self;
        [SerializeReference] public BlackboardVariable<float> SearchRadius = new(20f);

        // Outputs (written to blackboard)
        [SerializeReference] public BlackboardVariable<GameObject> TargetComponent;
        [SerializeReference] public BlackboardVariable<Vector3> TargetPosition;
        [SerializeReference] public BlackboardVariable<bool> IsAmbiguous;

        protected override Status OnStart()
        {
            // CompanionPuzzleObserver has already written TargetComponent to the blackboard.
            // This node validates and supplements that data.
            if (TargetComponent.Value == null) return Status.Failure;

            TargetPosition.Value = TargetComponent.Value.transform.position;
            return Status.Success;
        }
    }
}
