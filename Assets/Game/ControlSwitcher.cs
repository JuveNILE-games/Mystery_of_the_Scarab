using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Game.Events;
using Core.Utility.Attributes;
using Core;

public class ControlSwitcher : MonoBehaviour, IControlSwitcher
{
    [SerializeField] private ScriptableEventControlChanged onControlChanged;

    [Inject] private IControllableRegistry registry;
    [Inject] private IGameStateManager gameState;
    
    int currentIndex = 0;

    private bool _isInitialized = false;

    void Start()
    {
        if (gameState == null || gameState.CurrentState != GameState.SinglePlayer) { enabled = false; return; }
    }

    void Update()
    {
        if (!_isInitialized)
        {
            InitializeSwitching();
            _isInitialized = true;
            return;
        }

        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            var all = registry.GetAll();
            if (all.Count > 0)
            {
                int nextIndex = (currentIndex + 1) % all.Count;
                SwitchTo(nextIndex);
            }
        }
    }

    private void InitializeSwitching()
    {
        var all = registry.GetAll();
        if (all.Count == 0) return;
        
        for (int i = 0; i < all.Count; i++) ApplyState(all[i], i == currentIndex);
        
        // Ensure AI companions know who to follow from the start
        var playerTransform = all[currentIndex].GetTransform();
        foreach (var x in all)
        {
            var ai = x.GetTransform().GetComponentInChildren<IAIController>();
            if (ai != null) ai.UpdateBlackboardPlayer(playerTransform);
        }
    }

    public void SwitchTo(int newIndex)
    {
        var all = registry.GetAll();
        if (newIndex < 0 || newIndex >= all.Count || newIndex == currentIndex) return;
        
        ApplyState(all[currentIndex], false);
        ApplyState(all[newIndex], true);
        
        currentIndex = newIndex;
        
        // Update all AI companions to follow the new active player
        var playerTransform = all[currentIndex].GetTransform();
        foreach (var x in all)
        {
            var ai = x.GetTransform().GetComponent<IAIController>();
            if (ai != null) ai.UpdateBlackboardPlayer(playerTransform);
        }
        
        BroadcastControlChanged();
    }

    void ApplyState(IControllable c, bool isControlled)
    {
        if (isControlled) c.OnControlGained(); else c.OnControlLost();
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
