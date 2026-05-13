using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Unity.Behavior;
using Core.Utility.Attributes;
using Core.Systems.AgentNavigation;
using Core.Utility;
using Game.Systems.LevelSystem.Runtime;
using Game.Systems.PuzzleSystem;
using Game.Systems.PuzzleSystem.Runtime;
using Game.Systems.PuzzleSystem.Interfaces;

namespace Game.AI
{
    /// <summary>
    /// Subscribes to all PuzzleComponent events in the current room and
    /// updates the companion's blackboard with actionable puzzle targets.
    /// </summary>
    public class CompanionPuzzleObserver : MonoBehaviour
    {
        [Inject] private INavMeshSurfaceService _navMeshService;

        [Header("Config")]
        [SerializeField] private float _reactionDelayMin = 0.5f;
        [SerializeField] private float _reactionDelayMax = 1.5f;
        [SerializeField] private float _deferenceWindowDuration = 5f;
        [SerializeField] private float _reachabilityRadius = 20f;
        [SerializeField] private float _tieThreshold = 0.5f; // Distance threshold for ambiguity

        private BehaviorGraphAgent _behaviorAgent;
        private PlayerAbilities _abilities;
        private PuzzleRoomController _currentRoom;
        private float _deferenceTimer;

        // Cache all conditions we are currently listening to
        private readonly List<IPuzzleCondition> _subscribedConditions = new();
        
        // Per-session tracking: components this companion has attempted this room visit
        private readonly HashSet<string> _attemptedThisVisit = new();

        private void Awake()
        {
            _behaviorAgent = GetComponent<BehaviorGraphAgent>();
            _abilities = GetComponent<PlayerAbilities>();

            // Explicitly initialize the blackboard variable to 0
            if (_behaviorAgent != null)
            {
                _behaviorAgent.SetVariableValue("DeferenceTimer", 0f);
            }
        }

        // ── Control events from CompanionAIAdapter ──────────────────────

        public void OnPlayerReleasedControl()
        {
            // Player just let go — start the deference window.
            _deferenceTimer = _deferenceWindowDuration;
            if (_behaviorAgent != null)
                _behaviorAgent.SetVariableValue("DeferenceTimer", _deferenceTimer);
        }

        public void OnRoomEntered(PuzzleRoomController room)
        {
            UnsubscribeAll();
            _currentRoom = room;
            _attemptedThisVisit.Clear();
            
            if (_currentRoom != null)
            {
                _currentRoom.OnPuzzleUnlocked += OnPuzzleUnlocked;
                SubscribeToRoom(_currentRoom);
            }
        }

        private void OnPuzzleUnlocked(PuzzleController puzzle)
        {
            // A new puzzle unlocked (prerequisites met). Re-scan and subscribe.
            SubscribeToRoom(_currentRoom);
            EvaluatePuzzleState();
        }

        // ── Internal ────────────────────────────────────────────────────

        private void Update()
        {
            if (_deferenceTimer > 0f)
            {
                _deferenceTimer = Mathf.Max(0f, _deferenceTimer - Time.deltaTime);
                if (_behaviorAgent != null)
                {
                    _behaviorAgent.SetVariableValue("DeferenceTimer", _deferenceTimer);
                }
            }
        }

        private void SubscribeToRoom(PuzzleRoomController room)
        {
            // Subscribe to all current unsolved puzzles
            foreach (var puzzle in room.GetUnsolvedPuzzles())
            {
                foreach (var condition in puzzle.GetUnmetConditions())
                {
                    if (!_subscribedConditions.Contains(condition))
                    {
                        condition.OnConditionChanged += OnConditionChanged;
                        _subscribedConditions.Add(condition);
                    }
                }
            }
        }

        private void OnConditionChanged(IPuzzleCondition condition)
        {
            // A component just changed state — re-evaluate after a short delay
            StopAllCoroutines();
            StartCoroutine(EvaluateAfterDelay(0.1f));
        }

        private IEnumerator EvaluateAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            EvaluatePuzzleState();
        }

        private void EvaluatePuzzleState()
        {
            if (_currentRoom == null || _behaviorAgent == null) return;

            PuzzleComponent bestTarget = null;
            float bestDist = float.MaxValue;
            bool isAmbiguous = false;

            foreach (var puzzle in _currentRoom.GetUnsolvedPuzzles())
            {
                foreach (var condition in puzzle.GetUnmetConditions())
                {
                    if (condition is not PuzzleComponent component) continue;
                    
                    // Filter: Design-time eligibility
                    if (!component.IsAvailableForAI()) continue;
                    
                    // Filter: Prevent cycles
                    if (_attemptedThisVisit.Contains(condition.ConditionId)) continue;

                    // Filter: Ability Gate
                    var requiresAbility = component.GetComponent<RequiresAbility>();
                    if (requiresAbility != null && _abilities != null)
                    {
                        bool hasAbility = _abilities.abilities.Any(
                            a => a != null && a.data != null && a.data.abilityId == requiresAbility.requiredAbilityId && a.IsAvailable);
                        if (!hasAbility) continue;
                    }

                    // Filter: Player Agency
                    if (IsPlayerCovering(component)) continue;

                    // Filter: NavMesh Reachability
                    float dist = Vector3.Distance(transform.position, component.transform.position);
                    if (dist > _reachabilityRadius) continue;

                    if (!NavMesh.SamplePosition(component.transform.position,
                        out _, 2.0f, NavMesh.AllAreas)) continue;

                    // Ambiguity/Tie detection (§3)
                    if (Mathf.Abs(dist - bestDist) < _tieThreshold)
                    {
                        isAmbiguous = true;
                    }

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = component;
                        isAmbiguous = false; // Reset if we found a clear new winner
                    }
                }
            }

            if (bestTarget != null)
            {
                _behaviorAgent.SetVariableValue("TargetComponent", bestTarget.gameObject);
                _behaviorAgent.SetVariableValue("TargetPosition", bestTarget.transform.position);
                _behaviorAgent.SetVariableValue("HasActionablePuzzleTarget", true);
                
                // Holding logic: if we are close to the target and it requires holding, set IsHolding
                float distToTarget = Vector3.Distance(transform.position, bestTarget.transform.position);
                bool atTarget = distToTarget < 1.0f; // threshold for "being on it"
                bool shouldHold = bestTarget.RequiresHolding && atTarget;
                
                _behaviorAgent.SetVariableValue("IsHolding", shouldHold);
                _behaviorAgent.SetVariableValue<GameObject>("HoldTarget", shouldHold ? bestTarget.gameObject : null);
            }
            else
            {
                _behaviorAgent.SetVariableValue("HasActionablePuzzleTarget", false);
                _behaviorAgent.SetVariableValue("IsHolding", false);
                _behaviorAgent.SetVariableValue<GameObject>("HoldTarget", null);
            }
        }

        private bool IsPlayerCovering(PuzzleComponent component)
        {
            if (SceneCamera.Instance?.TrackingTarget.Value == null) return false;
            var playerPos = SceneCamera.Instance.TrackingTarget.Value.position;
            return Vector3.Distance(playerPos, component.transform.position) < 1.5f;
        }

        private void UnsubscribeAll()
        {
            if (_currentRoom != null)
            {
                _currentRoom.OnPuzzleUnlocked -= OnPuzzleUnlocked;
            }

            foreach (var c in _subscribedConditions)
            {
                if (c != null) c.OnConditionChanged -= OnConditionChanged;
            }
            _subscribedConditions.Clear();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
        }
    }
}
