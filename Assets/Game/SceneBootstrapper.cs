using UnityEngine;

using Core.Systems.Services;

public class SceneBootstrapper : MonoBehaviour, IRequireServices
{
    private IServiceLocator sceneLocator;
    private bool injectedByCore = false;

    void Awake()
    {
        var core = FindFirstObjectByType<Core.Bootstrapper>();
        if (core == null)
        {
            Debug.LogWarning("[SceneBootstrapper] No Bootstrapper found. Creating scene-local service locator (editor override).");
            sceneLocator = new ServiceLocator(); // Create isolated locator
            sceneLocator.Register<IGameStateManager>(new GameStateManagerImpl(GameState.SinglePlayer));
            sceneLocator.Register<IControllableRegistry>(new ControllableRegistry());
            OnInitialize(sceneLocator);
        }
    }

    public void InjectServices(IServiceLocator locator)
    {
        injectedByCore = true;
        
        // Create a scope from the global locator to allow local overrides
        if (locator is ServiceLocator concrete)
        {
            var scope = concrete.CreateScope();
            
            // Register Game/Scene specific services
            // These override or augment global services for this scene
            scope.Register<IGameStateManager>(new GameStateManagerImpl(GameState.SinglePlayer));
            scope.Register<IControllableRegistry>(new ControllableRegistry());
            // Add SceneLoader if needed locally, though checks suggested it was unused globally.
            // scope.Register<ISceneLoaderService>(new SceneLoaderService()); 
            
            sceneLocator = (IServiceLocator)scope; // ServiceScope implements IServiceLocator now
        }
        else
        {
            Debug.LogError("[SceneBootstrapper] Injected locator is not ServiceLocator! Cannot create scope.");
            sceneLocator = locator;
        }

        OnInitialize(sceneLocator);
    }

    protected virtual void OnInitialize(IServiceLocator locator)
    {
        // Scene-specific services
        Debug.Log("[SceneBootstrapper] Scene services initialized.");
    }

    void OnDestroy()
    {
        // If we created a scope (even if injected), we own it and should dispose it.
        // If injectedByCore is true, sceneLocator is likely a Scope.
        // If not, it's a standalone Locator. Both are IDisposable.
        
        if (sceneLocator != null) 
        {
            sceneLocator.Clear();
            if(sceneLocator is System.IDisposable d) d.Dispose();
        }
    }
}
