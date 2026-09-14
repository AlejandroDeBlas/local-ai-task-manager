using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Abstractions;

public interface ISystemMemoryProvider
{
    SystemMemorySnapshot GetSnapshot();
}
