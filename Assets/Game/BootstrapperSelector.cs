using UnityEngine;

public class BootstrapperSelector : MonoBehaviour
{
    public enum Mode { Auto, ForceSingle, ForceLocal, ForceOnline }
    public GameObject singlePlayerBootstrapperPrefab;
    public GameObject localMultiplayerBootstrapperPrefab;
    public GameObject onlineMultiplayerBootstrapperPrefab;
    public Mode mode = Mode.Auto;

    void Start()
    {
        Mode chosen = ChooseMode();
        InstantiateBootstrapper(chosen);
    }

    Mode ChooseMode()
    {
        if (mode == Mode.ForceSingle) return Mode.ForceSingle;
        if (mode == Mode.ForceLocal) return Mode.ForceLocal;
        if (mode == Mode.ForceOnline) return Mode.ForceOnline;
        // Auto: default to Single for safety
        return Mode.ForceSingle;
    }

    void InstantiateBootstrapper(Mode chosen)
    {
        GameObject prefab = null;
        switch (chosen)
        {
            case Mode.ForceSingle: prefab = singlePlayerBootstrapperPrefab; break;
            case Mode.ForceLocal: prefab = localMultiplayerBootstrapperPrefab; break;
            case Mode.ForceOnline: prefab = onlineMultiplayerBootstrapperPrefab; break;
        }
        if (prefab == null) { Debug.LogError($"[BootstrapperSelector] Prefab for {chosen} not assigned."); return; }
        var go = Instantiate(prefab);
        Debug.Log($"[BootstrapperSelector] Instantiated {prefab.name} (mode: {chosen}).");
    }
}
