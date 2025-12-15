using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Systems.Runner; // Added TaskRunner namespace
using Core.Systems.SceneManagement;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using PurrNet;
using UnityEngine;
using UnityEngine.UI;

namespace MultiPlayer.SceneManagement
{
    public class NetworkedSceneLoader : NetworkBehaviour
    {
        private readonly TaskRunner _taskRunner;
        
        [Header("Scriptable Events")]
        [SerializeField] private ScriptableEventString onSceneLoaded;
        [SerializeField] private ScriptableEventString onSceneUnloaded;
        [SerializeField] private ScriptableEventString onSceneGroupLoaded;
        [SerializeField] private ScriptableEventString onActiveSceneChanged;
        
        [SerializeField] private Image progressBar;
        [SerializeField] private float fillSpeed = 0.5f;
        [SerializeField] private Canvas loadingCanvas;
        [SerializeField] private Camera loadingCamera;
        [SerializeField] private SceneGroup[] sceneGroups;

        private float targetProgress;
        private bool isLoading;
        
        public NetworkedSceneGroupManager Manager;
        
        public NetworkedSceneLoader(TaskRunner taskRunner){
            this._taskRunner = taskRunner;
        }

        protected override void OnSpawned(){
            EnableLoadingCanvas(false);
            Manager = new NetworkedSceneGroupManager(onSceneLoaded, onSceneUnloaded, onSceneGroupLoaded, onActiveSceneChanged, networkManager.sceneModule);
            
            Manager.OnSceneLoaded.OnRaised += sceneName => Debug.Log($"Network Scene loaded: {sceneName}");
            Manager.OnSceneUnloaded.OnRaised += sceneName => Debug.Log($"Network Scene unloaded: {sceneName}");
            Manager.OnSceneGroupLoaded.OnRaised += sceneGroupName => Debug.Log($"Scene group loaded over network: {sceneGroupName}");
            Manager.OnActiveSceneChanged.OnRaised += activeSceneName => Debug.Log($"Active scene changed to: {activeSceneName}");
            base.OnSpawned();
        }
        
        [ContextMenu("Load Scene Group Networked")]
        private async void Load()
        {
            await LoadSceneGroup(0);
        }

        private void Update()
        {
            if (!isLoading) return;

            float currentFill = progressBar.fillAmount;
            float progressDiff = Mathf.Abs(currentFill - targetProgress);
            float dynamicSpeed = progressDiff * fillSpeed;

            progressBar.fillAmount = Mathf.Lerp(currentFill, targetProgress, Time.deltaTime * dynamicSpeed);
        }

        private async UniTask LoadSceneGroup(int index)
        {
            if (!IsValidSceneGroup(index)) return;

            ResetProgressBar();
            EnableLoadingCanvas(true);

            LoadingProgress progress = new LoadingProgress(value => targetProgress = Mathf.Max(value, targetProgress));
            CancellationToken cancellationToken;
            // Use TaskRunner to track this networked scene loading task
            var sceneLoadingTask = _taskRunner.AddTask(
                $"Loading networked scene group {sceneGroups[index].GroupName}",
                async (_,_) => {
                    await Manager.LoadScenes(sceneGroups[index], progress, cancellationToken);
                    return TaskResult<bool>.ForSuccess(true);
                },
                isBackground: false
            );
            
            // Subscribe to the OnCompleted event for better handling
            sceneLoadingTask.AddOnCompleted(result => {
                if (result.Success)
                {
                    // Scene loading completed successfully
                    Debug.Log($"Successfully loaded networked scene group: {sceneGroups[index].GroupName}");
                }
                else
                {
                    Debug.LogError($"Failed to load networked scene group {sceneGroups[index].GroupName}: {result.ErrorMessage}");
                }
            });
            
            // Wait for the scene loading to complete
            await sceneLoadingTask.CurrentTask;
            
            EnableLoadingCanvas(false);
        }

        private bool IsValidSceneGroup(int index)
        {
            if (index >= 0 && index < sceneGroups.Length) return true;
            Debug.LogError($"Invalid scene group index: {index}.");
            return false;
        }

        private void ResetProgressBar()
        {
            progressBar.fillAmount = 0;
            targetProgress = 1f;
        }

        private void EnableLoadingCanvas(bool enable)
        {
            isLoading = enable;
            loadingCanvas.enabled = enable;
            loadingCamera.enabled = enable;
            loadingCamera.GetComponent<AudioListener>().enabled = enable;
        }
        
        // New method to get the current number of running networked scene loading tasks
        public int GetRunningNetworkedSceneLoadingTasks()
        {
            return _taskRunner.GetRunningTaskCount();
        }
        
        // New method to wait for all networked scene loading tasks to complete
        public async UniTask WaitForAllNetworkedSceneLoadingTasks()
        {
            await _taskRunner.WaitForAllTasksAsync();
        }
    }

    public class LoadingProgress : IProgress<float>
    {
        private readonly Action<float> onProgress;

        public LoadingProgress(Action<float> progressCallback)
        {
            onProgress = progressCallback;
        }

        public void Report(float value)
        {
            onProgress?.Invoke(value);
        }
    }
}