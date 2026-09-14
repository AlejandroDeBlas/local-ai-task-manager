using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Abstractions;

public sealed record WorkloadDetectionContext(
    SystemSnapshot TelemetrySnapshot,
    IProcessRelationshipSnapshot ProcessRelationships
);
