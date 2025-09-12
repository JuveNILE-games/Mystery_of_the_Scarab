using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Systems.SceneManagement
{
    /// <summary>
    /// Abstract base class for scene group management, containing shared functionality
    /// between local and networked implementations.
    /// </summary>
    public abstract class BaseSceneGroupManager
    {
        public ScriptableEventString OnSceneLoaded { get; set; }
        public ScriptableEventString OnSceneUnloaded { get; set; }
        public ScriptableEventString OnSceneGroupLoaded { get; set; }
        public ScriptableEventString OnActiveSceneChanged { get; set; }

        /// <summary>
        /// Gets the list of currently loaded scenes.
        /// </summary>
        public List<Scene> GetCurrentlyLoadedScenes()
        {
            var loadedScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    loadedScenes.Add(scene);
                }
            }
            Debug.Log($"Currently loaded scenes: {string.Join(", ", loadedScenes.Select(s => s.name))}");
            return loadedScenes;
        }

        /// <summary>
        /// Gets the list of scenes that need to be unloaded when loading a new scene group.
        /// </summary>
        public List<Scene> GetScenesToUnload(SceneGroup newSceneGroup, List<Scene> currentlyLoadedScenes)
        {
            var scenesToUnload = new List<Scene>();
            
            // Always keep the bootstrap scene loaded
            foreach (Scene scene in currentlyLoadedScenes)
            {
                // If newSceneGroup is null, we're unloading all non-bootstrap scenes
                if (scene.name != "Bootstrapper" && (newSceneGroup == null || newSceneGroup.Scenes.All(s => s.Name != scene.name)))
                {
                    Debug.Log($"Scene {scene.name} marked for unloading");
                    scenesToUnload.Add(scene);
                }
                else
                {
                    Debug.Log($"Scene {scene.name} will be kept loaded");
                }
            }
            
            return scenesToUnload;
        }

        /// <summary>
        /// Sets the active scene.
        /// </summary>
        public void SetActiveScene(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid())
            {
                SceneManager.SetActiveScene(scene);
                OnActiveSceneChanged?.Raise(sceneName);
            }
        }

        /// <summary>
        /// Loads a scene group, unloading any scenes not part of the new group.
        /// </summary>
        public async Task LoadScenes(SceneGroup sceneGroup, IProgress<float> progress = null)
        {
            Debug.Log($"Loading scene group: {sceneGroup?.GroupName ?? "null"}");
            var currentlyLoadedScenes = GetCurrentlyLoadedScenes();
            Debug.Log($"Currently loaded scenes: {string.Join(", ", currentlyLoadedScenes.Select(s => s.name))}");
            var scenesToUnload = GetScenesToUnload(sceneGroup, currentlyLoadedScenes);
            Debug.Log($"Scenes to unload: {string.Join(", ", scenesToUnload.Select(s => s.name))}");
            
            // Unload scenes that are not part of the new group
            foreach (var scene in scenesToUnload)
            {
                Debug.Log($"Unloading scene: {scene.name}");
                await UnloadSceneAsync(scene.name);
                OnSceneUnloaded?.Raise(scene.name);
            }
            
            // Load scenes in the group
            if (sceneGroup != null)
            {
                for (int i = 0; i < sceneGroup.Scenes.Count; i++)
                {
                    var sceneData = sceneGroup.Scenes[i];
                    LoadSceneMode mode = sceneGroup.LoadMode;
                    
                    // Create a progress reporter for this specific scene
                    var sceneProgress = new Progress<float>(p => 
                    {
                        // Calculate overall progress based on number of scenes
                        float sceneWeight = (1.0f / sceneGroup.Scenes.Count);
                        float overallProgress = (i * sceneWeight) + (p * sceneWeight);
                        progress?.Report(overallProgress);
                    });
                    
                    Debug.Log($"Loading scene: {sceneData.Name} with mode: {mode}");
                    await LoadSceneAsync(sceneData, sceneProgress, mode);
                    OnSceneLoaded?.Raise(sceneData.Name);
                }
                
                // Set the first scene as active
                if (sceneGroup.Scenes.Count > 0)
                {
                    SetActiveScene(sceneGroup.Scenes[0].Name);
                }
                
                OnSceneGroupLoaded?.Raise(sceneGroup.GroupName);
            }
            
            progress?.Report(1.0f);
        }

        /// <summary>
        /// Unloads all currently loaded scenes except the bootstrap scene and persistent scenes.
        /// </summary>
        public async Task UnloadScenes()
        {
            var currentlyLoadedScenes = GetCurrentlyLoadedScenes();
            
            // Load the persistent scenes list from resources
            PersistentSceneList persistentScenes = Resources.Load<PersistentSceneList>("PersistentScenes");
            
            if (persistentScenes == null)
            {
                Debug.LogWarning("PersistentScenes asset not found in Resources. Only Bootstrap scene will be preserved.");
            }
            
            // Unload all scenes except bootstrap and persistent scenes
            foreach (var scene in currentlyLoadedScenes)
            {
                // Always keep the bootstrap scene loaded
                if (scene.name == "Bootstrapper")
                {
                    Debug.Log($"Skipping unload of Bootstrap scene: {scene.name}");
                    continue;
                }
                
                // Skip persistent scenes
                if (persistentScenes && persistentScenes.IsPersistentScene(scene))
                {
                    Debug.Log($"Skipping unload of persistent scene: {scene.name}");
                    continue;
                }
                
                Debug.Log($"Unloading scene: {scene.name}");
                await UnloadSceneAsync(scene.name);
                OnSceneUnloaded?.Raise(scene.name);
            }
        }

        /// <summary>
        /// Abstract method for loading a scene, to be implemented by derived classes.
        /// </summary>
        protected abstract Task LoadSceneAsync(SceneData sceneData, IProgress<float> progress, LoadSceneMode mode = LoadSceneMode.Additive);

        /// <summary>
        /// Abstract method for unloading a scene, to be implemented by derived classes.
        /// </summary>
        protected abstract Task UnloadSceneAsync(string sceneName);
    }
}