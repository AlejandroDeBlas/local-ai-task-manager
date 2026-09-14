namespace LocalAITaskManager.Core.Models;

public sealed record DetectionEvidence(
    DetectionEvidenceKind Kind,
    string Description
);

public sealed record DetectedModelIdentity(
    string? DisplayName,
    string? ModelPath,
    string? Quantization,
    string? Architecture,
    string? ParameterSize,
    int? ContextLength,
    ulong? RuntimeReportedVramBytes = null
);

public sealed record AiProcessIdentity(
    int Pid,
    AiRuntimeKind Runtime,
    DetectionConfidence RuntimeConfidence,
    DetectedModelIdentity? Model,
    DetectionConfidence ModelConfidence,
    IReadOnlyList<DetectionEvidence> Evidence,
    int? RuntimeRootPid = null
);

public sealed record RuntimeModelObservation(
    AiRuntimeKind Runtime,
    DetectedModelIdentity Model,
    ulong? RuntimeReportedVramBytes = null
);

public sealed record DetectionWarning(
    string Source,
    string Message
);

public sealed record DetectionSnapshot(
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<int, AiProcessIdentity> Processes,
    IReadOnlyList<RuntimeModelObservation> UnmappedModels,
    IReadOnlyList<DetectionWarning> Warnings
);

public sealed record RuntimeDetectionResult(
    IReadOnlyList<AiProcessIdentity> IdentifiedProcesses,
    IReadOnlyList<RuntimeModelObservation> UnmappedModels,
    IReadOnlyList<DetectionWarning> Warnings
);
