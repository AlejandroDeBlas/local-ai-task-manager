using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Abstractions;

public interface IWorkloadDetectionCoordinator
{
    Task<DetectionSnapshot> DetectWorkloadsAsync(
        SystemSnapshot snapshot,
        CancellationToken cancellationToken);
}
