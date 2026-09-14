using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Services;

public sealed record AggregatedProcessMemory(
    int Pid,
    ulong? LocalBytes,
    ulong? NonLocalBytes,
    ulong? TotalCommittedBytes,
    ulong? DedicatedBytes,
    ulong? SharedBytes
);

public static class GpuProcessMemoryAggregator
{
    public static ulong? AddNullable(ulong? a, ulong? b)
    {
        if (!a.HasValue)
        {
            return b;
        }

        if (!b.HasValue)
        {
            return a;
        }

        return a.Value + b.Value;
    }

    public static IReadOnlyList<AggregatedProcessMemory> AggregateByPid(IEnumerable<GpuProcessMemorySample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var map = new Dictionary<int, (ulong? Local, ulong? NonLocal, ulong? TotalCommitted, ulong? Dedicated, ulong? Shared)>();

        foreach (var sample in samples)
        {
            if (map.TryGetValue(sample.Pid, out var existing))
            {
                map[sample.Pid] = (
                    AddNullable(existing.Local, sample.LocalBytes),
                    AddNullable(existing.NonLocal, sample.NonLocalBytes),
                    AddNullable(existing.TotalCommitted, sample.TotalCommittedBytes),
                    AddNullable(existing.Dedicated, sample.DedicatedBytes),
                    AddNullable(existing.Shared, sample.SharedBytes)
                );
            }
            else
            {
                map[sample.Pid] = (
                    sample.LocalBytes,
                    sample.NonLocalBytes,
                    sample.TotalCommittedBytes,
                    sample.DedicatedBytes,
                    sample.SharedBytes
                );
            }
        }

        var result = new List<AggregatedProcessMemory>(map.Count);
        foreach (var kvp in map)
        {
            result.Add(new AggregatedProcessMemory(
                kvp.Key,
                kvp.Value.Local,
                kvp.Value.NonLocal,
                kvp.Value.TotalCommitted,
                kvp.Value.Dedicated,
                kvp.Value.Shared
            ));
        }

        return result;
    }
}
