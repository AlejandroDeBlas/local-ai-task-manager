namespace LocalAITaskManager.Core.Abstractions;

using LocalAITaskManager.Core.Models;

public interface IAiWorkloadComposer
{
    IReadOnlyList<AiWorkloadSnapshot> Compose(
        SystemSnapshot telemetry,
        DetectionSnapshot detection);
}
