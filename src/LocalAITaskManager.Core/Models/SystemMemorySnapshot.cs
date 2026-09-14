namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Immutable snapshot representing the physical RAM state of the operating system.
/// Values are null when memory telemetry cannot be queried.
/// </summary>
public sealed record SystemMemorySnapshot(
    ulong? TotalPhysicalBytes,
    ulong? UsedPhysicalBytes,
    ulong? AvailablePhysicalBytes
);
