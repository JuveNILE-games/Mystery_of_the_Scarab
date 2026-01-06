using UnityEngine;
using System.Collections.Generic;
using Game.Events;

public class ControlSwitcher : MonoBehaviour, IRequireServices
{
    [SerializeField] private ScriptableEventControlChanged onControlChanged;

    IServiceLocator locator;
    IControllableRegistry registry;
    IGameStateManager gameState;
    int currentIndex = 0;

    public void InjectServices(IServiceLocator locator)
    {
        this.locator = locator;
        registry = locator.Get<IControllableRegistry>();
        gameState = locator.Get<IGameStateManager>();
    }

    void Start()
    {
        if (gameState.CurrentState != GameState.SinglePlayer) { enabled = false; return; }
        var all = registry.GetAll();
        if (all.Count == 0) enabled = false;
        for (int i = 0; i < all.Count; i++) ApplyState(all[i], i == currentIndex);
    }

    public void SwitchTo(int newIndex)
    {
        var all = registry.GetAll();
        if (newIndex < 0 || newIndex >= all.Count || newIndex == currentIndex) return;
        ApplyState(all[currentIndex], false);
        ApplyState(all[newIndex], true);
        currentIndex = newIndex;
        BroadcastControlChanged();
    }

    void ApplyState(IControllable c, bool isControlled)
    {
        if (isControlled) c.OnControlGained(); else c.OnControlLost();
        var all = registry.GetAll();
        var playerTransform = all[currentIndex].GetTransform();
        foreach (var x in all) if (x is IAIController ai) ai.UpdateBlackboardPlayer(playerTransform);
    }

    void BroadcastControlChanged()
    {
        var all = registry.GetAll();
        var tf = all[currentIndex].GetTransform();
        
        if (onControlChanged != null)
        {
            onControlChanged.Raise(new ControlChanged { newIndex = currentIndex, newTransform = tf });
        }
    }
}
