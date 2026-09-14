namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Immutable snapshot representing a process utilizing GPU memory and system resources.
/// </summary>
public sealed record GpuProcessSnapshot(
    int Pid,
    string ProcessName,
    string? ExecutablePath,
    string? CommandLine,
    ulong WorkingSetBytes,
    double? CpuPercent,
    ulong? DedicatedGpuMemoryBytes,
    ulong? SharedGpuMemoryBytes
);
