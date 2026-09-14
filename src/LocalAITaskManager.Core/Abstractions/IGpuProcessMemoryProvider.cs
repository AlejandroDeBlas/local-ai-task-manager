using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Abstractions;

public interface IGpuProcessMemoryProvider
{
    Task<IReadOnlyList<GpuProcessMemorySample>> GetProcessMemoryAsync(CancellationToken cancellationToken = default);
}
