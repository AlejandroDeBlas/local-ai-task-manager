namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Raw memory sample extracted from a performance counter instance before PID aggregation.
/// </summary>
public sealed record GpuProcessMemorySample(
    int Pid,
    ulong DedicatedBytes,
    ulong SharedBytes,
    int? PhysicalAdapterIndex,
    long? Luid
);
