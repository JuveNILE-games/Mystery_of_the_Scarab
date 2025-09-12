// Modified: Assets/Core/Systems/Runner/TaskRunner.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityUtils;

namespace Core.Systems.Runner{
    public class TaskRunner : PersistentSingleton<TaskRunner>
    {
        [SerializeField] private List<IRunningTask> runningTasks = new List<IRunningTask>();
        private readonly object lockObject = new object(); // For thread safety

        public RunningTask<T> AddTask<T>(string description, Func<Task<TaskResult<T>>> taskFunc, Action<TaskResult<T>> onCompleted = null, bool isBackground = true)
        {
            var runningTask = new RunningTask<T>(description, taskFunc, onCompleted, isBackground);
            lock (lockObject)
            {
                runningTasks.Add(runningTask);
            }
            // Remove task from list when it completes (thread-safe)
            _ = Task.Run(async () => {
                try
                {
                    await runningTask.CurrentTask;
                }
                finally
                {
                    lock (lockObject)
                    {
                        runningTasks.Remove(runningTask);
                    }
                }
            });
            return runningTask;
        }
        
        public RunningTask<T> AddTask<T>(RunningTask<T> runningTask)
        {
            lock (lockObject)
            {
                runningTasks.Add(runningTask);
            }
            // Remove task from list when it completes (thread-safe)
            _ = Task.Run(async () => {
                try
                {
                    await runningTask.CurrentTask;
                }
                finally
                {
                    lock (lockObject)
                    {
                        runningTasks.Remove(runningTask);
                    }
                }
            });
            return runningTask;
        }
        

        public async Task WaitForAllTasksAsync()
        {
            Task[] tasksCopy;
            lock (lockObject)
            {
                tasksCopy = runningTasks.Select(rt => rt.Task).ToArray();
            }
            
            // Wait for all tasks to complete
            if (tasksCopy.Length > 0)
            {
                await Task.WhenAll(tasksCopy);
            }
        }
        
        public int GetRunningTaskCount()
        {
            lock (lockObject)
            {
                return runningTasks.Count;
            }
        }
    }
}