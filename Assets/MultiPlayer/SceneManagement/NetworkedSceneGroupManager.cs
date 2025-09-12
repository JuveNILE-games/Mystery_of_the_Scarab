using System;
using System.Threading.Tasks;
using Core.Systems.SceneManagement;
using Obvious.Soap;
using PurrNet.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiPlayer.SceneManagement
{
    public class NetworkedSceneGroupManager : BaseSceneGroupManager
    {
        private readonly ScenesModule _scenesModule;
        
        public NetworkedSceneGroupManager(ScriptableEventString onSceneLoaded, ScriptableEventString onSceneUnloaded,
            ScriptableEventString onSceneGroupLoaded, ScriptableEventString onActiveSceneChanged, ScenesModule scenesModule)
        {
            OnSceneLoaded = onSceneLoaded;
            OnSceneUnloaded = onSceneUnloaded;
            OnSceneGroupLoaded = onSceneGroupLoaded;
            OnActiveSceneChanged = onActiveSceneChanged;
            _scenesModule = scenesModule;
        }
        
        protected override async Task LoadSceneAsync(SceneData sceneData, IProgress<float> progress, LoadSceneMode mode = LoadSceneMode.Additive)
        {
            var sceneSettings = new PurrSceneSettings 
            { 
                isPublic = false, 
                mode = mode
            };
            
            var operation = _scenesModule.LoadSceneAsync(sceneData.Reference.Name, sceneSettings);
            
            // Proper progress monitoring instead of arbitrary delays
            while (!operation.isDone)
            {
                progress?.Report(operation.progress);
                await Task.Yield();
            }
        }
        
        protected override async Task UnloadSceneAsync(string sceneName)
        {
            // Network-specific unload logic
            var operation = _scenesModule.UnloadSceneAsync(sceneName);
            
            // Handle case where operation is null (scene might already be unloaded or invalid)
            if (operation != null)
            {
                await operation;
            }
            else
            {
                Debug.LogWarning($"Failed to unload network scene '{sceneName}'. Scene may already be unloaded or invalid.");
            }
        }
    }
}