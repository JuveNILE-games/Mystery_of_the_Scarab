using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(PlayerInteractor))]
public class CompanionAI : MonoBehaviour, IAIController
{
    public Transform playerTransform;
    NavMeshAgent agent;
    PlayerInteractor selfInteractor;
    PlayerAbilities selfAbilities;
    public float decisionInterval = 0.25f;
    public float assistSearchRadius = 12f;
    public float followDistance = 2f;
    public float maxApproachDistance = 30f;
    public float interactionStopDistance = 1.2f;

    Interactable currentTarget;
    Coroutine decisionRoutine;
    float stuckTimer;
    Vector3 lastAgentPos;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        selfInteractor = GetComponent<PlayerInteractor>();
        selfAbilities = GetComponent<PlayerAbilities>();
    }

    void OnEnable() { decisionRoutine = StartCoroutine(DecisionLoop()); }
    void OnDisable() { if (decisionRoutine != null) StopCoroutine(decisionRoutine); }

    IEnumerator DecisionLoop()
    {
        while (true)
        {
            EvaluateAndAct();
            yield return new WaitForSeconds(decisionInterval);
        }
    }

    void EvaluateAndAct()
    {
        if (currentTarget != null)
        {
            if (!Interactable.All.Contains(currentTarget) || Vector3.Distance(transform.position, currentTarget.transform.position) > maxApproachDistance) { ClearCurrentTarget(); }
            else
            {
                float dist = Vector3.Distance(transform.position, GetApproachPoint(currentTarget));
                if (dist <= interactionStopDistance) { TryInteractWith(currentTarget); }
                else { if (agent.destination != GetApproachPoint(currentTarget)) agent.SetDestination(GetApproachPoint(currentTarget)); }
                CheckIfStuck();
                if (stuckTimer > 2f) ClearCurrentTarget();
                return;
            }
        }

        var candidates = Interactable.All.Where(i => Vector3.Distance(transform.position, i.transform.position) <= assistSearchRadius).ToArray();
        Interactable best = null; float bestScore = float.NegativeInfinity;
        foreach (var cand in candidates)
        {
            float score = ScoreCandidate(cand);
            if (score > bestScore) { bestScore = score; best = cand; }
        }
        if (best != null && bestScore > 0.1f) { SetCurrentTarget(best); return; }
        FollowPlayer();
    }

    float ScoreCandidate(Interactable cand)
    {
        float score = 0f;
        var candPos = cand.transform.position;
        float dist = Vector3.Distance(transform.position, candPos);
        score += -1f * dist;
        if (playerTransform != null) { float distToPlayer = Vector3.Distance(playerTransform.position, candPos); if (distToPlayer < 4f) score += 40f; }
        var req = cand.GetComponent<RequiresAbility>();
        if (req != null) { if (HasAbility(req.requiredAbilityId)) score += 80f; else score -= 50f; }
        else score += 20f;
        score += 1f / (dist + 0.01f);
        return score;
    }

    bool HasAbility(string abilityId)
    {
        if (selfAbilities == null) return false;
        foreach (var a in selfAbilities.abilities) if (a != null && a.data != null && a.data.abilityId == abilityId) return true;
        return false;
    }

    void SetCurrentTarget(Interactable target)
    {
        currentTarget = target;
        Vector3 approach = GetApproachPoint(target);
        agent.isStopped = false;
        agent.SetDestination(approach);
        lastAgentPos = agent.transform.position;
        stuckTimer = 0f;
    }

    void ClearCurrentTarget() { currentTarget = null; if (agent != null) agent.ResetPath(); }

    Vector3 GetApproachPoint(Interactable target) { return target.transform.position; }

    void TryInteractWith(Interactable target)
    {
        var req = target.GetComponent<RequiresAbility>();
        if (req != null && !string.IsNullOrEmpty(req.requiredAbilityId))
        {
            var ability = FindAbilityById(req.requiredAbilityId);
            if (ability != null && ability.IsAvailable) { ability.TryUse(); return; }
            else { target.Interact(selfInteractor); return; }
        }
        target.Interact(selfInteractor);
    }

    AbilityBehaviour FindAbilityById(string id)
    {
        if (selfAbilities == null) return null;
        return selfAbilities.abilities.Find(a => a != null && a.data != null && a.data.abilityId == id);
    }

    void FollowPlayer()
    {
        if (playerTransform == null) return;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist > followDistance + 0.5f) { agent.isStopped = false; Vector3 dir = (transform.position - playerTransform.position).normalized; Vector3 goal = playerTransform.position + dir * followDistance; agent.SetDestination(goal); }
        else agent.isStopped = true;
    }

    void CheckIfStuck()
    {
        if (agent == null || !agent.hasPath) return;
        float moved = Vector3.Distance(agent.transform.position, lastAgentPos);
        if (moved < 0.01f) stuckTimer += Time.deltaTime; else { stuckTimer = 0f; lastAgentPos = agent.transform.position; }
    }

    // IAIController
    public void EnableAI(bool enabled) { this.enabled = enabled; if (agent != null) agent.enabled = enabled; }
    public void UpdateBlackboardPlayer(Transform playerTransform) { this.playerTransform = playerTransform; }

    // Optional external request
    public void RequestHelpAt(Vector3 worldPos) { agent.SetDestination(worldPos); currentTarget = null; }
}
