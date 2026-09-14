namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Top-level immutable snapshot containing unified system telemetry at a point in time.
/// </summary>
public sealed record SystemSnapshot(
    DateTimeOffset Timestamp,
    IReadOnlyList<GpuDeviceSnapshot> Gpus,
    SystemMemorySnapshot Memory,
    IReadOnlyList<GpuProcessSnapshot> GpuProcesses,
    IReadOnlyList<TelemetryWarning> Warnings
);
