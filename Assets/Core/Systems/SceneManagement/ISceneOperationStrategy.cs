using System;
using System.Threading.Tasks;
using Core.Systems.SceneManagement;
using UnityEngine.SceneManagement;

namespace Core.Systems.SceneManagement
{
    /// <summary>
    /// Interface for scene operation strategies, allowing different implementations
    /// for local and networked scene operations.
    /// </summary>
    public interface ISceneOperationStrategy
    {
        /// <summary>
        /// Loads a scene asynchronously.
        /// </summary>
        Task LoadSceneAsync(SceneData sceneData, IProgress<float> progress, LoadSceneMode mode);
        
        /// <summary>
        /// Unloads a scene asynchronously.
        /// </summary>
        Task UnloadSceneAsync(string sceneName);
    }
}