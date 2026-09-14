using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Abstractions;

public interface IGpuTelemetryProvider
{
    Task<IReadOnlyList<GpuDeviceSnapshot>> GetGpusAsync(CancellationToken cancellationToken = default);
}
