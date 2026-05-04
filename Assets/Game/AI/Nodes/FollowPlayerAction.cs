using System;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes{
    /// <summary>
    /// Unity Behavior action: follows the player with a "leash" buffer to prevent twitchy movement.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Follow Player",
        story: "[Agent] follows [Player] at [FollowDistance] Start: [StartDistance] Stop: [StopDistance]",
        category: "Companion/Actions",
        id: "companion_follow_player")]
    public class FollowPlayerAction : Action
    {
        [Header("Distances")]
        [SerializeReference] public BlackboardVariable<float> StartDistance = new(4f);
        [SerializeReference] public BlackboardVariable<float> FollowDistance = new(2f);
        [SerializeReference] public BlackboardVariable<float> StopDistance = new(1.5f);

        [Header("References")]
        [SerializeReference] public BlackboardVariable<NavMeshAgent> Agent = new();
        [SerializeReference] public BlackboardVariable<Transform> Player = new();

        private NavMeshAgent _agent;
        private Transform _player;
        private bool _isFollowing;

        protected override Status OnStart()
        {
            _agent = Agent.Value;
            _player = Player.Value;

            if (_agent == null || _player == null)
            {
                var bg = GameObject.GetComponent<BehaviorGraphAgent>();
                if (bg != null)
                {
                    if (_agent == null && bg.GetVariable<NavMeshAgent>("Agent", out var bbAgent))
                        _agent = bbAgent.Value;
                    if (_player == null && bg.GetVariable<Transform>("Player", out var bbPlayer))
                        _player = bbPlayer.Value;
                }
            }

            if (_agent == null || _player == null)
            {
                Debug.LogWarning("[FollowPlayerAction] OnStart: could not resolve Agent or Player.");
                return Status.Failure;
            }

            _isFollowing = false;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent.pathPending) return Status.Running;
            if (_agent.pathStatus == NavMeshPathStatus.PathInvalid) return Status.Failure;

            float dist = Vector3.Distance(_agent.transform.position, _player.position);

            if (!_isFollowing)
            {
                if (dist > StartDistance.Value)
                {
                    _isFollowing = true;
                    _agent.isStopped = false;
                    _agent.SetDestination(_player.position);
                }
            }
            else
            {
                if (dist <= StopDistance.Value)
                {
                    _isFollowing = false;
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
                else
                {
                    _agent.SetDestination(_player.position);
                }
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            if (_agent != null)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
            }
            _isFollowing = false;
        }
    }
}