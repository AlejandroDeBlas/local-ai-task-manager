namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Immutable snapshot representing a process utilizing GPU memory and system resources.
/// All memory metrics are nullable to distinguish 0 bytes from unavailable telemetry.
/// </summary>
public sealed record GpuProcessSnapshot(
    int Pid,
    string ProcessName,
    string? ExecutablePath,
    string? CommandLine,
    ulong? WorkingSetBytes,
    double? CpuPercent,
    ulong? LocalGpuMemoryBytes,
    ulong? NonLocalGpuMemoryBytes,
    ulong? TotalCommittedGpuMemoryBytes,
    ulong? DedicatedGpuMemoryBytes,
    ulong? SharedGpuMemoryBytes
);
