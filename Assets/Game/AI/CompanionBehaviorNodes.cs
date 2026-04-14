using System;
using UnityEngine;
using UnityEngine.AI;
using Unity.Behavior;
using Action = Unity.Behavior.Action;

namespace Game.AI
{
    /// <summary>
    /// Unity Behavior condition: checks if the player is within a specified range of the companion.
    /// </summary>
    [Serializable]
    [Condition(
        name: "Is Player In Range",
        story: "Is [Player] within [Range] of [Self]",
        category: "Companion")]
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

    /// <summary>
    /// Unity Behavior action: moves the NavMeshAgent to a target position and completes when within stop distance.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Move To Position",
        story: "Move [Agent] to [TargetPosition]",
        category: "Companion",
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

    /// <summary>
    /// Unity Behavior action: finds the closest Interactable within a search radius and outputs it
    /// along with its position for navigation.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Find Closest Interactable",
        story: "Find closest interactable within [SearchRadius] of [Self]",
        category: "Companion",
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

    /// <summary>
    /// Unity Behavior action: interacts with the target Interactable using the companion's PlayerInteractor.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Interact With Target",
        story: "Interact with [Target] using [Interactor]",
        category: "Companion",
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

    /// <summary>
    /// Unity Behavior action: follows the player by navigating toward them,
    /// stopping at a specified follow distance.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Follow Player",
        story: "[Agent] follows [Player] at [FollowDistance]",
        category: "Companion",
        id: "companion_follow_player")]
    public class FollowPlayerAction : Action
    {
        [SerializeReference] public BlackboardVariable<NavMeshAgent> Agent;
        [SerializeReference] public BlackboardVariable<Transform> Player;
        [SerializeReference] public BlackboardVariable<float> FollowDistance = new(2f);

        protected override Status OnStart()
        {
            if (Agent.Value == null || Player.Value == null) return Status.Failure;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Agent.Value == null || Player.Value == null) return Status.Failure;

            float dist = Vector3.Distance(Agent.Value.transform.position, Player.Value.position);
            if (dist <= FollowDistance.Value)
            {
                Agent.Value.isStopped = true;
                return Status.Success;
            }

            Agent.Value.isStopped = false;
            Agent.Value.SetDestination(Player.Value.position);
            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (Agent.Value != null)
            {
                Agent.Value.isStopped = true;
            }
        }
    }

    /// <summary>
    /// Unity Behavior action: uses a specific ability on the companion character.
    /// Requires a PlayerAbilities component and an ability ID to activate.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Use Ability",
        story: "Use ability [AbilityId] on [Abilities]",
        category: "Companion",
        id: "companion_use_ability")]
    public class UseAbilityAction : Action
    {
        [SerializeReference] public BlackboardVariable<PlayerAbilities> Abilities;
        [SerializeReference] public BlackboardVariable<string> AbilityId;

        protected override Status OnStart()
        {
            if (Abilities.Value == null || string.IsNullOrEmpty(AbilityId.Value))
                return Status.Failure;

            var ability = Abilities.Value.abilities.Find(
                a => a != null && a.data != null && a.data.abilityId == AbilityId.Value);

            if (ability == null || !ability.IsAvailable)
                return Status.Failure;

            ability.TryUse();
            return Status.Success;
        }
    }
}
