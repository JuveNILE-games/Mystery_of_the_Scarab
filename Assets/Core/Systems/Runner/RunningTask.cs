// Modified: Assets/Core/Systems/Runner/RunningTask.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Systems.Runner{
    public class RunningTask<T> : IRunningTask
    {
        public string Description { get; }
        public Task<TaskResult<T>> CurrentTask { get; }
        Task IRunningTask.Task => CurrentTask;
        public Action<TaskResult<T>> OnCompleted { get; private set; } = delegate { };

        public RunningTask(string description, Func<Task<TaskResult<T>>> taskFunc, Action<TaskResult<T>> onCompleted = null, bool isBackground = true)
        {
            Description = description;
            
            // Wrap the task function with exception handling
            if (isBackground)
            {
                CurrentTask = Task.Run(async () => {
                    try
                    {
                        return await taskFunc();
                    }
                    catch (Exception ex)
                    {
                        // Convert exceptions to failure results
                        return TaskResult<T>.ForFailure($"Task '{description}' failed with exception: {ex.Message}");
                    }
                });
            }
            else
            {
                CurrentTask = taskFunc();
            }
            
            OnCompleted += onCompleted;
            _ = CurrentTask.ContinueWith(t => {
                if (t.IsCompletedSuccessfully)
                {
                    OnCompleted?.Invoke(t.Result);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        
        public void AddOnCompleted(Action<TaskResult<T>> onCompleted)
        {
            OnCompleted += onCompleted;
        }
        
        public void RemoveOnCompleted(Action<TaskResult<T>> onCompleted)
        {
            OnCompleted -= onCompleted;
        }
    }
}