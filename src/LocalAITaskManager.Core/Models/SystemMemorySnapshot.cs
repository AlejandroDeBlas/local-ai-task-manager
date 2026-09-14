namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Immutable snapshot representing the physical RAM state of the operating system.
/// </summary>
public sealed record SystemMemorySnapshot(
    ulong TotalPhysicalBytes,
    ulong UsedPhysicalBytes,
    ulong AvailablePhysicalBytes
);
