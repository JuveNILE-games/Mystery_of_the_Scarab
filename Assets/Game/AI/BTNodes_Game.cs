using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using Core.BT;

namespace Game.AI
{
    // Condition: is player in range
    public class Condition_PlayerInRange : BTNode
    {
        public float range;
        public Condition_PlayerInRange(float range) : base("Cond_PlayerInRange") { this.range = range; }
        public override BTNodeState Tick(Blackboard blackboard)
        {
            if (!blackboard.TryGet<Transform>("selfTransform", out var self) || !blackboard.TryGet<Transform>("playerTransform", out var player))
                return BTNodeState.Failure;
            float d = Vector3.Distance(self.position, player.position);
            return d <= range ? BTNodeState.Success : BTNodeState.Failure;
        }
    }

    // Action: MoveTo target position (blackboard key "moveTarget": Vector3)
    public class Action_MoveToPosition : BTNode
    {
        public float stopDistance = 1f;
        public Action_MoveToPosition(float stopDistance = 1f) : base("MoveToPos") { this.stopDistance = stopDistance; }
        public override BTNodeState Tick(Blackboard blackboard)
        {
            if (!blackboard.TryGet<NavMeshAgent>("agent", out var agent)) return BTNodeState.Failure;
            if (!blackboard.TryGet<Vector3>("moveTarget", out var target)) return BTNodeState.Failure;
            if (agent.pathPending) return BTNodeState.Running;
            agent.SetDestination(target);
            float dist = Vector3.Distance(agent.transform.position, target);
            if (dist <= stopDistance) { agent.ResetPath(); return BTNodeState.Success; }
            return BTNodeState.Running;
        }
    }

    // Action: FindClosestInteractable within radius, set "targetInteractable" on blackboard
    public class Action_FindClosestInteractable : BTNode
    {
        float searchRadius;
        public Action_FindClosestInteractable(float radius = 12f) : base("FindClosestInteractable") { searchRadius = radius; }
        public override BTNodeState Tick(Blackboard blackboard)
        {
            if (!blackboard.TryGet<Transform>("selfTransform", out var self)) return BTNodeState.Failure;
            Interactable best = null; float bestScore = float.MaxValue;
            foreach (var i in Interactable.All)
            {
                var d = Vector3.Distance(self.position, i.transform.position);
                if (d <= searchRadius && d < bestScore) { bestScore = d; best = i; }
            }
            if (best != null) { blackboard.Set("targetInteractable", best); blackboard.Set("moveTarget", best.transform.position); return BTNodeState.Success; }
            return BTNodeState.Failure;
        }
    }

    // Action: Interact with current blackboard "targetInteractable"
    public class Action_InteractTarget : BTNode
    {
        public Action_InteractTarget() : base("InteractTarget") { }
        public override BTNodeState Tick(Blackboard blackboard)
        {
            if (!blackboard.TryGet<PlayerInteractor>("selfInteractor", out var self)) return BTNodeState.Failure;
            if (!blackboard.TryGet<Interactable>("targetInteractable", out var target)) return BTNodeState.Failure;
            target.Interact(self);
            return BTNodeState.Success;
        }
    }

    // Action: Use ability with id
    public class Action_UseAbilityOnTarget : BTNode
    {
        string abilityId;
        public Action_UseAbilityOnTarget(string abilityId) : base("UseAbility:" + abilityId) { this.abilityId = abilityId; }
        public override BTNodeState Tick(Blackboard blackboard)
        {
            if (!blackboard.TryGet<PlayerAbilities>("abilities", out var abilities)) return BTNodeState.Failure;
            if (!blackboard.TryGet<PlayerInteractor>("selfInteractor", out var self)) return BTNodeState.Failure;
            var ab = abilities.abilities.FirstOrDefault(a => a != null && a.data != null && a.data.abilityId == abilityId);
            if (ab == null) return BTNodeState.Failure;
            if (!ab.IsAvailable) return BTNodeState.Failure;
            ab.TryUse();
            return BTNodeState.Success;
        }
    }

    // Action: Follow player (set moveTarget to player's position)
    public class Action_FollowPlayer : BTNode
    {
        float stopDistance;
        public Action_FollowPlayer(float stopDistance = 2f) : base("FollowPlayer") { this.stopDistance = stopDistance; }
        public override BTNodeState Tick(Blackboard blackboard)
        {
            if (!blackboard.TryGet<Transform>("playerTransform", out var player) || !blackboard.TryGet<NavMeshAgent>("agent", out var agent)) return BTNodeState.Failure;
            float dist = Vector3.Distance(agent.transform.position, player.position);
            if (dist <= stopDistance) { agent.ResetPath(); return BTNodeState.Success; }
            blackboard.Set("moveTarget", player.position);
            agent.SetDestination(player.position);
            return BTNodeState.Running;
        }
    }
}
