using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes{
    /// <summary>
    /// Unity Behavior action: interacts with the target Interactable using the companion's PlayerInteractor.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Interact With Target",
        story: "Interact with [Target] using [Interactor]",
        category: "Companion/Actions",
        id: "companion_interact_with_target")]
    public class InteractWithTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<PlayerInteractor> Interactor;

        protected override Status OnStart()
        {
            if (Target.Value == null || Interactor.Value == null)
                return Status.Failure;

            var interactable = Target.Value.GetComponent<Interactable>();
            if (interactable == null) return Status.Failure;

            interactable.Interact(Interactor.Value);
            return Status.Success;
        }
    }
}