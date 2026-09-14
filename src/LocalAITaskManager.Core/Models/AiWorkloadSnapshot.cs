namespace LocalAITaskManager.Core.Models;

public sealed record AiWorkloadSnapshot(
    string WorkloadId,
    AiWorkloadKind Kind,
    AiRuntimeKind Runtime,
    string DisplayName,
    int PrimaryPid,
    int? RuntimeRootPid,
    DetectedModelIdentity? Model,
    DetectionConfidence RuntimeConfidence,
    DetectionConfidence ModelConfidence,
    IReadOnlyList<int> ProcessPids,
    ulong? PrimaryLocalGpuMemoryBytes,
    ulong? PrimaryWorkingSetBytes,
    double? PrimaryCpuPercent,
    IReadOnlyList<DetectionEvidence> Evidence
);
