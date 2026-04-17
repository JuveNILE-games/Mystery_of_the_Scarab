using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Game.Events;
using Core.Utility.Attributes;
using Core;
using Core.Utility;
using Core.Systems.AgentNavigation;
using Core.Systems.Services;

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
        
        // Ensure reactive systems (Camera, AI) know who to follow from the start
        if (SceneCamera.Instance != null)
        {
            SceneCamera.Instance.TrackingTarget.Value = all[currentIndex].GetTransform();
        }

        // Initialize the dynamic NavMesh surface around the AI companion (inactive player)
        if (ServiceLocator.Global.TryGet<INavMeshSurfaceService>(out var navMeshService))
        {
            for (int j = 0; j < all.Count; j++)
            {
                if (j != currentIndex)
                {
                    navMeshService.InitializeSurface(all[j].GetTransform());
                    break;
                }
            }
        }
    }

    public void SwitchTo(int newIndex)
    {
        var all = registry.GetAll();
        if (newIndex < 0 || newIndex >= all.Count || newIndex == currentIndex) return;

        // 1. Publish the new target to the global reactive system (Camera and AI will react automatically)
        if (SceneCamera.Instance != null)
        {
            SceneCamera.Instance.TrackingTarget.Value = all[newIndex].GetTransform();
        }

        // 2. Re-anchor the NavMesh surface on the AI companion (the previously active player)
        if (ServiceLocator.Global.TryGet<INavMeshSurfaceService>(out var navMeshService))
        {
            navMeshService.InitializeSurface(all[currentIndex].GetTransform());
        }

        // 3. Then apply control states
        ApplyState(all[currentIndex], false);
        ApplyState(all[newIndex], true);
        
        currentIndex = newIndex;
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
