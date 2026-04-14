using UnityEngine;
using UnityEngine.AI;
using Unity.Behavior;

namespace Game.AI
{
    /// <summary>
    /// Adapter that allows the core game systems to control a companion character
    /// that is driven by the Unity Behavior graph system.
    /// Implements the standard IAIController interface.
    /// </summary>
    [RequireComponent(typeof(BehaviorGraphAgent))]
    public class CompanionAIAdapter : MonoBehaviour, IAIController
    {
        private BehaviorGraphAgent _behaviorAgent;
        private NavMeshAgent _navAgent;
        private PlayerInteractor _interactor;
        private PlayerAbilities _abilities;

        private void Awake()
        {
            _behaviorAgent = GetComponent<BehaviorGraphAgent>();
            _navAgent = GetComponent<NavMeshAgent>();
            _interactor = GetComponent<PlayerInteractor>();
            _abilities = GetComponent<PlayerAbilities>();
            
            SetupBlackboard();
        }

        private void SetupBlackboard()
        {
            if (_behaviorAgent == null) return;

            // Automatically bind local components to Blackboard variables if they exist
            _behaviorAgent.SetVariableValue("Self", gameObject);
            
            if (_navAgent != null) _behaviorAgent.SetVariableValue("Agent", _navAgent);
            if (_interactor != null) _behaviorAgent.SetVariableValue("Interactor", _interactor);
            if (_abilities != null) _behaviorAgent.SetVariableValue("Abilities", _abilities);
        }

        public void EnableAI(bool enabled)
        {
            if (_behaviorAgent != null)
            {
                _behaviorAgent.enabled = enabled;
            }

            if (_navAgent != null)
            {
                _navAgent.enabled = enabled;
                if (enabled)
                {
                    if (_navAgent.isOnNavMesh)
                    {
                        _navAgent.isStopped = false;
                    }
                }
            }
        }

        public void UpdateBlackboardPlayer(Transform playerTransform)
        {
            if (_behaviorAgent != null && _behaviorAgent.Graph != null)
            {
                // Requires the Unity Behavior graph to have a blackboard variable named "Player" of type Transform
                _behaviorAgent.SetVariableValue("Player", playerTransform);
            }
        }
    }
}
