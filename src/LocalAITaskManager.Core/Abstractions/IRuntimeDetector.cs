using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Abstractions;

public interface IRuntimeDetector
{
    AiRuntimeKind Runtime { get; }

    Task<RuntimeDetectionResult> DetectAsync(
        WorkloadDetectionContext context,
        CancellationToken cancellationToken);
}
