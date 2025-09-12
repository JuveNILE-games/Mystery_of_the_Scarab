using System.Collections.Generic;
using System.Linq;
using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "PersistentSceneList", menuName = "Core/Scriptable Objects/PersistentSceneList")]
public class PersistentSceneList : ScriptableObject
{
    [SerializeField] private List<SceneReference> scenes = new List<SceneReference>();
    
    public bool IsPersistentScene(Scene scene){
        bool isPersistent = scenes.Any(reference => SceneManager.GetSceneByBuildIndex(reference.BuildIndex) == scene);
        if (isPersistent)
        {
            Debug.Log($"Scene {scene.name} is persistent (matched by build index)");
        }
        return isPersistent;
    }
    
    public bool IsPersistentScene(string sceneName){
        bool isPersistent = scenes.Any(reference => reference.Name == sceneName);
        if (isPersistent)
        {
            Debug.Log($"Scene {sceneName} is persistent (matched by name)");
        }
        return isPersistent;
    }
}