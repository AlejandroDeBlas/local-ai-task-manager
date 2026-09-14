using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;
using Xunit;

namespace LocalAITaskManager.Core.Tests;

public class GpuProcessMemoryAggregatorTests
{
    [Fact]
    public void AggregateByPid_CombinesMultipleSamplesForSamePid()
    {
        // PID 100 -> 500 MB dedicated
        // PID 100 -> 300 MB dedicated
        // Expected: PID 100 -> 800 MB dedicated
        ulong mb500 = 500UL * 1024UL * 1024UL;
        ulong mb300 = 300UL * 1024UL * 1024UL;

        var samples = new List<GpuProcessMemorySample>
        {
            new(Pid: 100, DedicatedBytes: mb500, SharedBytes: 1000, PhysicalAdapterIndex: 0, Luid: 1),
            new(Pid: 100, DedicatedBytes: mb300, SharedBytes: 2000, PhysicalAdapterIndex: 1, Luid: 2)
        };

        var aggregated = GpuProcessMemoryAggregator.AggregateByPid(samples);

        Assert.Single(aggregated);
        var entry = aggregated[0];
        Assert.Equal(100, entry.Pid);
        Assert.Equal(800UL * 1024UL * 1024UL, entry.DedicatedBytes);
        Assert.Equal(3000UL, entry.SharedBytes);
    }

    [Fact]
    public void AggregateByPid_HandlesMultipleDifferentPids()
    {
        var samples = new List<GpuProcessMemorySample>
        {
            new(Pid: 101, DedicatedBytes: 100, SharedBytes: 10, PhysicalAdapterIndex: 0, Luid: 1),
            new(Pid: 102, DedicatedBytes: 200, SharedBytes: 20, PhysicalAdapterIndex: 0, Luid: 1),
            new(Pid: 101, DedicatedBytes: 300, SharedBytes: 30, PhysicalAdapterIndex: 0, Luid: 1)
        };

        var aggregated = GpuProcessMemoryAggregator.AggregateByPid(samples);

        Assert.Equal(2, aggregated.Count);

        var p101 = Assert.Single(aggregated, x => x.Pid == 101);
        Assert.Equal(400UL, p101.DedicatedBytes);
        Assert.Equal(40UL, p101.SharedBytes);

        var p102 = Assert.Single(aggregated, x => x.Pid == 102);
        Assert.Equal(200UL, p102.DedicatedBytes);
        Assert.Equal(20UL, p102.SharedBytes);
    }

    [Fact]
    public void AggregateByPid_EmptyInput_ReturnsEmptyList()
    {
        var aggregated = GpuProcessMemoryAggregator.AggregateByPid([]);
        Assert.Empty(aggregated);
    }
}
