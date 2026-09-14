namespace LocalAITaskManager.Core.Models;

public sealed record AppSnapshot(
    SystemSnapshot Telemetry,
    DetectionSnapshot Detection,
    IReadOnlyList<AiWorkloadSnapshot> Workloads
);
