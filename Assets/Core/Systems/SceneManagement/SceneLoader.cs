// Modified: Assets/Core/Systems/SceneManagement/SceneLoader.cs
using System;
using System.Threading.Tasks;
using Core.Systems.Navigation;
using Core.Systems.Runner; // Added TaskRunner namespace
using Obvious.Soap;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Systems.SceneManagement
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private ScreenNavigator screenNavigator;
        
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
        
        public SceneGroupManager Manager; 

        private void Awake()
        {
            Manager = new SceneGroupManager(onSceneLoaded, onSceneUnloaded, onSceneGroupLoaded, onActiveSceneChanged);
            
            Manager.OnSceneLoaded.OnRaised += sceneName => Debug.Log($"Scene loaded: {sceneName}");
            Manager.OnSceneUnloaded.OnRaised += sceneName => Debug.Log($"Scene unloaded: {sceneName}");
            Manager.OnSceneGroupLoaded.OnRaised += sceneGroupName => Debug.Log($"Scene group loaded: {sceneGroupName}");
            Manager.OnActiveSceneChanged.OnRaised += activeSceneName => Debug.Log($"Active scene changed to: {activeSceneName}");
        }

        private async void Start()
        {
            if (Bootstrapper.IsEditMode && Bootstrapper.sceneOverrideName != "Bootstrapper") return;
            // load all scenegroups one by one
            for (int i = 0; i < sceneGroups.Length; i++)
            {
                await LoadSceneGroup(i);
            }
        }

        private void Update()
        {
            if (!isLoading) return;

            float currentFill = progressBar.fillAmount;
            float progressDiff = Mathf.Abs(currentFill - targetProgress);
            float dynamicSpeed = progressDiff * fillSpeed;

            progressBar.fillAmount = Mathf.Lerp(currentFill, targetProgress, Time.deltaTime * dynamicSpeed);
        }

        private async Task LoadSceneGroup(int index)
        {
            if (!IsValidSceneGroup(index)) return;

            ResetProgressBar();
            EnableLoadingCanvas(true);

            var progress = new LoadingProgress(value => targetProgress = Mathf.Max(value, targetProgress));
            
            // Use TaskRunner to track this scene loading task
            var sceneLoadingTask = TaskRunner.Instance.AddTask(
                $"Loading scene group {sceneGroups[index].GroupName}",
                async () => {
                    await Manager.LoadScenes(sceneGroups[index], progress);
                    return TaskResult<bool>.ForSuccess(true);
                },
                isBackground: false
            );
            
            // Subscribe to the OnCompleted event for better handling
            sceneLoadingTask.AddOnCompleted(result => {
                if (result.Success)
                {
                    // Automatically navigate to EntryScreen if defined
                    SceneData entrySceneData = sceneGroups[index].GetSceneDataByType(SceneType.ActiveScene);
                    if (entrySceneData != null && entrySceneData.EntryScreen != null && screenNavigator != null)
                    {
                        screenNavigator.NavigateTo(entrySceneData.EntryScreen);
                    }
                }
                else
                {
                    Debug.LogError($"Failed to load scene group {sceneGroups[index].GroupName}: {result.ErrorMessage}");
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
        
        public async void OverrideSceneGroup(SceneGroup[] overrideGroups)
        {
            if (overrideGroups == null || overrideGroups.Length == 0)
            {
                Debug.LogWarning("No scene groups to override.");
                return;
            }
            
            sceneGroups = overrideGroups;
            
            // Use TaskRunner to track the unloading task
            var unloadTask = TaskRunner.Instance.AddTask(
                "Unloading scenes",
                async () => {
                    await Manager.UnloadScenes();
                    return TaskResult<bool>.ForSuccess(true);
                },
                isBackground: false
            );
            
            // Subscribe to the OnCompleted event for better handling
            unloadTask.AddOnCompleted(result => {
                if (!result.Success)
                {
                    Debug.LogError($"Failed to unload scenes: {result.ErrorMessage}");
                }
            });
            
            await unloadTask.CurrentTask;
            
            if (unloadTask.CurrentTask.IsCompletedSuccessfully && unloadTask.CurrentTask.Result.Success)
            {
                await LoadSceneGroup(0);
            }
        }
        
        // New method to get the current number of running scene loading tasks
        public int GetRunningSceneLoadingTasks()
        {
            return TaskRunner.Instance.GetRunningTaskCount();
        }
        
        // New method to wait for all scene loading tasks to complete
        public async Task WaitForAllSceneLoadingTasks()
        {
            await TaskRunner.Instance.WaitForAllTasksAsync();
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