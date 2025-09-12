using System.Threading.Tasks;

namespace Core.Systems.Runner {
    public interface IRunningTask
    {
        string Description { get; }
        Task Task { get; }
    }
}