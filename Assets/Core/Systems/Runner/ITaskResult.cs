namespace Core.Systems.Runner {
    public interface ITaskResult
    {
        bool Success { get; }
        bool Failed { get; }
        string ErrorMessage { get; }
    }

    public interface ITaskResult<out T> : ITaskResult
    {
        T Result { get; }
    }

    public struct TaskResult<T> : ITaskResult<T>
    {
        public bool Success => !Failed;
        public bool Failed { get; private set; }
        public string ErrorMessage { get; private set; }
        public T Result { get; private set; }

        public static TaskResult<T> ForSuccess(T result)
        {
            return new TaskResult<T> { Result = result, Failed = false };
        }

        public static TaskResult<T> ForFailure(string errorMessage)
        {
            return new TaskResult<T> { Failed = true, ErrorMessage = errorMessage };
        }
    }
}