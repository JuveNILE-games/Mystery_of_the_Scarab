using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Systems.SceneManagement
{
    public class SceneGroupManager : BaseSceneGroupManager
    {
        public SceneGroupManager(ScriptableEventString onSceneLoaded, ScriptableEventString onSceneUnloaded, 
            ScriptableEventString onSceneGroupLoaded, ScriptableEventString onActiveSceneChanged)
        {
            OnSceneLoaded = onSceneLoaded;
            OnSceneUnloaded = onSceneUnloaded;
            OnSceneGroupLoaded = onSceneGroupLoaded;
            OnActiveSceneChanged = onActiveSceneChanged;
        }
        
        protected override async Task LoadSceneAsync(SceneData sceneData, IProgress<float> progress, LoadSceneMode mode = LoadSceneMode.Additive)
        {
            var operation = SceneManager.LoadSceneAsync(sceneData.Reference.Path, mode);
            
            // Proper progress monitoring instead of arbitrary delays
            while (!operation.isDone)
            {
                progress?.Report(operation.progress);
                await Task.Yield();
            }
        }
        
        protected override async Task UnloadSceneAsync(string sceneName)
        {
            var operation = SceneManager.UnloadSceneAsync(sceneName);
            
            // Handle case where operation is null (scene might already be unloaded or invalid)
            if (operation != null)
            {
                await operation;
            }
            else
            {
                Debug.LogWarning($"Failed to unload scene '{sceneName}'. Scene may already be unloaded or invalid.");
            }
        }
    }
}