using System;
using Core;
using Core.Boot;
using UnityEngine;
using Core.Systems.Services;
using Core.Systems.Services.Interfaces;

public class SceneBootstrapper : MonoBehaviour, IRequireServices
{
    private ServiceLocator _isolatedLocator; // only set in editor fallback
    private IServiceScope _sceneScope;

    void Awake()
    {
        var core = FindFirstObjectByType<Bootstrapper>();
        if (core != null) return;

        Debug.LogWarning("[SceneBootstrapper] No Bootstrapper found. Creating scene-local service locator (editor override).");
        _isolatedLocator = ServiceLocator.CreateIsolated();
        _sceneScope = _isolatedLocator.CreateScope();
        RegisterSceneServices(_sceneScope);
        OnInitialize(_sceneScope);
    }

    public void InjectServices(IServiceLocator locator)
    {
        if (locator is ServiceLocator concrete)
        {
            _sceneScope = concrete.CreateScope();
        }
        else
        {
            Debug.LogError("[SceneBootstrapper] Injected locator is not ServiceLocator! Cannot create scope.");
            _sceneScope = locator.CreateScope();
        }

        RegisterSceneServices(_sceneScope);
        OnInitialize(_sceneScope);
    }

    // Single place for scene service registrations — fixes the DRY issue from the plan
    protected virtual void RegisterSceneServices(IServiceScope scope)
    {
        // Global services (GameStateManager, ControllableRegistry) are now handled by MysteryOfTheScarabInstaller
    }

    protected virtual void OnInitialize(IServiceScope scope)
    {
        Debug.Log("[SceneBootstrapper] Scene services initialized.");
    }

    void OnDestroy()
    {
        _sceneScope?.Dispose();
        _sceneScope = null;

        // Only dispose the isolated locator if we created one (editor fallback path)
        _isolatedLocator?.Dispose();
        _isolatedLocator = null;
    }
}