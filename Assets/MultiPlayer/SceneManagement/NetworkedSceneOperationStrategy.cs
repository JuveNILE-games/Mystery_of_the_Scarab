using System;
using System.Threading.Tasks;
using Core.Systems.SceneManagement;
using PurrNet.Modules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MultiPlayer.SceneManagement
{
    /// <summary>
    /// Networked scene operation strategy for multiplayer scene management.
    /// </summary>
    public class NetworkedSceneOperationStrategy : ISceneOperationStrategy
    {
        private readonly ScenesModule _scenesModule;
        
        public NetworkedSceneOperationStrategy(ScenesModule scenesModule)
        {
            _scenesModule = scenesModule;
        }
        
        public async Task LoadSceneAsync(SceneData sceneData, IProgress<float> progress, LoadSceneMode mode)
        {
            var sceneSettings = new PurrSceneSettings 
            { 
                isPublic = false, 
                mode = mode
            };
            
            AsyncOperation operation = _scenesModule.LoadSceneAsync(sceneData.Reference.Name, sceneSettings);
            
            // Proper progress monitoring instead of arbitrary delays
            while (!operation.isDone)
            {
                progress?.Report(operation.progress);
                await Task.Yield();
            }
        }
        
        public async Task UnloadSceneAsync(string sceneName)
        {
            // Network-specific unload logic
            AsyncOperation operation = _scenesModule.UnloadSceneAsync(sceneName);
            await operation;
        }
    }
}