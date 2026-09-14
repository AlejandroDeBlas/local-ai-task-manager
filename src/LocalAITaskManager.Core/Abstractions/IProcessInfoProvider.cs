using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Abstractions;

public interface IProcessInfoProvider
{
    ProcessMetadata GetMetadata(int pid);
    void Cleanup(IReadOnlyCollection<int> currentPids);
}
