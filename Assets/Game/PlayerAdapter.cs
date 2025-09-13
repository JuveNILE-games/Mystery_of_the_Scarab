using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(PlayerInteractor))]
public class PlayerAdapter : MonoBehaviour, IControllable
{
    public NavMeshAgent agent;
    public MonoBehaviour btRunner;
    public PlayerInteractor interactor;
    public PlayerAbilities abilities;

    void Reset() { interactor = GetComponent<PlayerInteractor>(); }

    public Transform GetTransform() => transform;

    public void OnControlGained()
    {
        if (agent != null) { agent.enabled = false; }
        if (btRunner != null) btRunner.enabled = false;
        if (interactor != null) interactor.SetControlled(true);
        if (abilities != null) abilities.OnControlGained();
    }

    public void OnControlLost()
    {
        if (agent != null) { agent.enabled = true; agent.Warp(transform.position); agent.isStopped = false; }
        if (btRunner != null) btRunner.enabled = true;
        if (interactor != null) interactor.SetControlled(false);
        if (abilities != null) abilities.OnControlLost();
    }

    public void UpdateAIPlayerReference(Transform player)
    {
        var ai = GetComponent<IAIController>();
        if (ai != null) ai.UpdateBlackboardPlayer(player);
    }
}
