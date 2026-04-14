using UnityEngine;
using Core.Systems.Session;

/// <summary>
/// Selects and instantiates the appropriate bootstrapper based on the desired session type.
/// Updated to use the unified SessionType enum.
/// </summary>
public class BootstrapperSelector : MonoBehaviour
{
    [Header("Bootstrapper Prefabs")]
    public GameObject singlePlayerBootstrapperPrefab;
    public GameObject localMultiplayerBootstrapperPrefab;
    public GameObject onlineMultiplayerBootstrapperPrefab;

    [Header("Test Settings")]
    public bool forceSessionType = false;
    public SessionType forcedType = SessionType.Solo;

    private void Start()
    {
        SessionType chosen = ChooseSessionType();
        InstantiateBootstrapper(chosen);
    }

    private SessionType ChooseSessionType()
    {
        if (forceSessionType) return forcedType;
        
        // Auto logic: default to Solo unless a session has been prepared
        // (This will be expanded in Phase 1 when ISessionService is implemented)
        return SessionType.Solo;
    }

    private void InstantiateBootstrapper(SessionType type)
    {
        GameObject prefab = null;
        switch (type)
        {
            case SessionType.Solo:
                prefab = singlePlayerBootstrapperPrefab;
                break;
            case SessionType.SplitScreen:
            case SessionType.LAN:
                prefab = localMultiplayerBootstrapperPrefab;
                break;
            case SessionType.Online:
                prefab = onlineMultiplayerBootstrapperPrefab;
                break;
        }

        if (prefab == null)
        {
            Debug.LogError($"[BootstrapperSelector] Prefab for {type} not assigned.");
            return;
        }

        var go = Instantiate(prefab);
        go.name = $"[Bootstrapper] {type}";
        Debug.Log($"[BootstrapperSelector] Instantiated {prefab.name} for {type} session.");
    }
}
