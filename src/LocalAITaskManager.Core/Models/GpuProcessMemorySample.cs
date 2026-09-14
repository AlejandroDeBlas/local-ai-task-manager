namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Raw memory sample extracted from a performance counter instance before PID aggregation.
/// Contains the 5 WDDM GPU Process Memory counters as nullable values.
/// </summary>
public sealed record GpuProcessMemorySample(
    int Pid,
    ulong? LocalBytes,
    ulong? NonLocalBytes,
    ulong? TotalCommittedBytes,
    ulong? DedicatedBytes,
    ulong? SharedBytes,
    int? PhysicalAdapterIndex,
    long? Luid
);
