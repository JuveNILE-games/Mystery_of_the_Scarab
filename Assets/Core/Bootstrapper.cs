using Core.Utility;
using Eflatun.SceneReference;
#if UNITY_EDITOR
using Core.Systems.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core{
    public class Bootstrapper : PersistentSingleton<Bootstrapper>{
        public static string sceneOverrideName;
        public static bool IsEditMode => Application.isEditor;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init(){
            sceneOverrideName = SceneManager.GetActiveScene().name;
            Debug.Log("Bootstrapper initializing...");
            SceneManager.LoadScene("Bootstrapper", LoadSceneMode.Single);
        }
#if UNITY_EDITOR
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(){
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    Debug.Log("Starting Play Mode in Scene: " + sceneOverrideName);
                }
            };
        }

        private static void Update(){
            if (SceneManager.GetActiveScene().name == "Bootstrapper")
            {
                Debug.Log("Bootstrapper scene is active, loading scene override: " + sceneOverrideName);
                //Find the SceneLoader component in the scene
                SceneLoader sceneLoader = FindAnyObjectByType<SceneLoader>();
                SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneManager.GetActiveScene().path.Replace("Bootstrapper", sceneOverrideName));
                SceneData sceneData = new SceneData(new SceneReference(scene), sceneOverrideName);
                SceneGroup sceneGroupOverride = new SceneGroup(sceneOverrideName,  sceneData);
                sceneLoader.OverrideSceneGroup(new[] { sceneGroupOverride });
                EditorApplication.update -= Update; // Unsubscribe to avoid repeated calls
            }
        }


#endif
    }
}