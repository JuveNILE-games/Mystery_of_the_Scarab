using UnityEngine;
using UnityEngine.AI;
using Core.BT;

namespace Game.AI
{
    [RequireComponent(typeof(NavMeshAgent), typeof(PlayerInteractor))]
    public class CompanionBehaviorBuilder : MonoBehaviour
    {
        public Transform player;
        public float assistRadius = 10f;
        BTRunner runner;

        void Start()
        {
            runner = gameObject.AddComponent<BTRunner>();
            var bb = new Blackboard();
            bb.Set("selfTransform", transform);
            bb.Set("playerTransform", player);
            bb.Set("agent", GetComponent<NavMeshAgent>());
            bb.Set("selfInteractor", GetComponent<PlayerInteractor>());
            var abilities = GetComponent<PlayerAbilities>();
            if (abilities != null) bb.Set("abilities", abilities);
            runner.blackboard = bb;
            
            var find = new Action_FindClosestInteractable(assistRadius);
            var move = new Action_MoveToPosition(1.2f);
            var interact = new Action_InteractTarget();
            var seqAssist = new SequenceNode(find, move, interact);
            var follow = new Action_FollowPlayer(2f);
            
            runner.root = new Selector(seqAssist, follow);
            runner.tickInterval = 0.2f;
        }
    }
}
