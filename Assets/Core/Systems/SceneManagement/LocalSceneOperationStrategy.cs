using System;
using System.Threading.Tasks;
using Core.Systems.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Systems.SceneManagement
{
    /// <summary>
    /// Local scene operation strategy for single-player scene management.
    /// </summary>
    public class LocalSceneOperationStrategy : ISceneOperationStrategy
    {
        public async Task LoadSceneAsync(SceneData sceneData, IProgress<float> progress, LoadSceneMode mode)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneData.Reference.Path, mode);
            
            // Proper progress monitoring instead of arbitrary delays
            while (!operation.isDone)
            {
                progress?.Report(operation.progress);
                await Task.Yield();
            }
        }
        
        public async Task UnloadSceneAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.UnloadSceneAsync(sceneName);
            await operation;
        }
    }
}