using UnityEngine;

public class SceneBootstrapper : MonoBehaviour, IRequireServices
{
    private SimpleServiceLocator sceneLocator;
    private bool injectedByCore = false;

    void Awake()
    {
        var core = FindFirstObjectByType<CoreBootstrapper>();
        if (core == null)
        {
            Debug.LogWarning("[SceneBootstrapper] No CoreBootstrapper found. Creating scene-local service locator (editor override).");
            sceneLocator = new SimpleServiceLocator();
            sceneLocator.Register<IGameStateManager>(new GameStateManagerImpl(GameState.SinglePlayer));
            sceneLocator.Register<IControllableRegistry>(new ControllableRegistry());
            sceneLocator.Register<LocalEventBusImpl>(new LocalEventBusImpl());
            sceneLocator.Register<IInputService>(new InputServiceImpl());
            OnInitialize(sceneLocator);
        }
    }

    public void InjectServices(IServiceLocator locator)
    {
        injectedByCore = true;
        sceneLocator = locator as SimpleServiceLocator;
        OnInitialize(sceneLocator);
    }

    protected virtual void OnInitialize(SimpleServiceLocator locator)
    {
        // Scene-specific services
        Debug.Log("[SceneBootstrapper] Scene services initialized.");
    }

    void OnDestroy()
    {
        if (!injectedByCore && sceneLocator != null) sceneLocator.Clear();
    }
}
