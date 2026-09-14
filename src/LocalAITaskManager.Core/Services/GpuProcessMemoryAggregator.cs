using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Services;

public sealed record AggregatedProcessMemory(
    int Pid,
    ulong DedicatedBytes,
    ulong SharedBytes
);

public static class GpuProcessMemoryAggregator
{
    public static IReadOnlyList<AggregatedProcessMemory> AggregateByPid(IEnumerable<GpuProcessMemorySample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        var map = new Dictionary<int, (ulong Dedicated, ulong Shared)>();

        foreach (var sample in samples)
        {
            if (map.TryGetValue(sample.Pid, out var existing))
            {
                map[sample.Pid] = (
                    existing.Dedicated + sample.DedicatedBytes,
                    existing.Shared + sample.SharedBytes
                );
            }
            else
            {
                map[sample.Pid] = (sample.DedicatedBytes, sample.SharedBytes);
            }
        }

        var result = new List<AggregatedProcessMemory>(map.Count);
        foreach (var kvp in map)
        {
            result.Add(new AggregatedProcessMemory(kvp.Key, kvp.Value.Dedicated, kvp.Value.Shared));
        }

        return result;
    }
}
