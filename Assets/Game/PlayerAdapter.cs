using Core.Utility.Attributes;
using Game.Player;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(PlayerInteractor))]
public class PlayerAdapter : MonoBehaviour, IControllable
{
    public PlayerInputInitializer input;
    public NavMeshAgent agent;
    public PlayerInteractor interactor;
    public PlayerAbilities abilities;
    [Inject] private IControllableRegistry _registry;

    private void Start()
    {
        _registry?.Register(this);
    }

    private void OnDisable()
    {
        _registry?.Unregister(this);
    }

    public Transform GetTransform() => transform;

    public void OnControlGained()
    {
        var ai = GetComponentInChildren<IAIController>();
        if (ai != null) ai.EnableAI(false);
        
        if (input == null) input = GetComponentInChildren<PlayerInputInitializer>();
        if (input != null) input.enabled = true;

        if (interactor != null) interactor.SetControlled(true);
        if (abilities != null) abilities.OnControlGained();
    }

    public void OnControlLost()
    {
        var ai = GetComponentInChildren<IAIController>();
        if (ai != null) ai.EnableAI(true);

        if (input == null) input = GetComponentInChildren<PlayerInputInitializer>();
        if (input != null)
        {
            input.ClearInputState();
            input.enabled = false;
        }

        if (interactor != null) interactor.SetControlled(false);
        if (abilities != null) abilities.OnControlLost();
    }

    public void UpdateAIPlayerReference(Transform player)
    {
        var ai = GetComponentInChildren<IAIController>();
        if (ai != null) ai.UpdateBlackboardPlayer(player);
    }
}
