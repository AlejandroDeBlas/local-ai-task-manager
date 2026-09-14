using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;
using Xunit;

namespace LocalAITaskManager.Core.Tests;

public class GpuProcessMemoryAggregatorTests
{
    [Fact]
    public void AddNullable_NormalValues_ReturnsSum()
    {
        ulong? result = GpuProcessMemoryAggregator.AddNullable(500UL, 300UL);
        Assert.Equal(800UL, result);
    }

    [Fact]
    public void AddNullable_OneNull_ReturnsOtherValue()
    {
        ulong? result1 = GpuProcessMemoryAggregator.AddNullable(null, 300UL);
        Assert.Equal(300UL, result1);

        ulong? result2 = GpuProcessMemoryAggregator.AddNullable(500UL, null);
        Assert.Equal(500UL, result2);
    }

    [Fact]
    public void AddNullable_BothNull_ReturnsNull()
    {
        ulong? result = GpuProcessMemoryAggregator.AddNullable(null, null);
        Assert.Null(result);
    }

    [Fact]
    public void AddNullable_RealZero_PreservesZero()
    {
        ulong? result1 = GpuProcessMemoryAggregator.AddNullable(0UL, 0UL);
        Assert.Equal(0UL, result1);

        ulong? result2 = GpuProcessMemoryAggregator.AddNullable(0UL, null);
        Assert.Equal(0UL, result2);

        ulong? result3 = GpuProcessMemoryAggregator.AddNullable(null, 0UL);
        Assert.Equal(0UL, result3);
    }

    [Fact]
    public void AggregateByPid_CombinesMultipleSamplesForSamePidAcrossAllCounters()
    {
        ulong mb500 = 500UL * 1024UL * 1024UL;
        ulong mb300 = 300UL * 1024UL * 1024UL;

        var samples = new List<GpuProcessMemorySample>
        {
            new(
                Pid: 100,
                LocalBytes: mb500,
                NonLocalBytes: 100,
                TotalCommittedBytes: 600,
                DedicatedBytes: mb500,
                SharedBytes: 1000,
                PhysicalAdapterIndex: 0,
                Luid: 1
            ),
            new(
                Pid: 100,
                LocalBytes: mb300,
                NonLocalBytes: 200,
                TotalCommittedBytes: 500,
                DedicatedBytes: mb300,
                SharedBytes: 2000,
                PhysicalAdapterIndex: 1,
                Luid: 2
            )
        };

        var aggregated = GpuProcessMemoryAggregator.AggregateByPid(samples);

        Assert.Single(aggregated);
        var entry = aggregated[0];
        Assert.Equal(100, entry.Pid);
        Assert.Equal(800UL * 1024UL * 1024UL, entry.LocalBytes);
        Assert.Equal(300UL, entry.NonLocalBytes);
        Assert.Equal(1100UL, entry.TotalCommittedBytes);
        Assert.Equal(800UL * 1024UL * 1024UL, entry.DedicatedBytes);
        Assert.Equal(3000UL, entry.SharedBytes);
    }

    [Fact]
    public void AggregateByPid_NullableHandling_PreservesNullWhenAllAreNull()
    {
        var samples = new List<GpuProcessMemorySample>
        {
            new(100, LocalBytes: null, NonLocalBytes: 50, TotalCommittedBytes: null, DedicatedBytes: 100, SharedBytes: null, 0, 1),
            new(100, LocalBytes: 300, NonLocalBytes: null, TotalCommittedBytes: null, DedicatedBytes: null, SharedBytes: null, 1, 2)
        };

        var aggregated = GpuProcessMemoryAggregator.AggregateByPid(samples);

        Assert.Single(aggregated);
        var entry = aggregated[0];
        Assert.Equal(100, entry.Pid);
        Assert.Equal(300UL, entry.LocalBytes);
        Assert.Equal(50UL, entry.NonLocalBytes);
        Assert.Null(entry.TotalCommittedBytes); // Both samples had null TotalCommitted -> null, never 0
        Assert.Equal(100UL, entry.DedicatedBytes);
        Assert.Null(entry.SharedBytes); // Both samples had null Shared -> null
    }

    [Fact]
    public void AggregateByPid_HandlesMultipleDifferentPids()
    {
        var samples = new List<GpuProcessMemorySample>
        {
            new(101, LocalBytes: 100, NonLocalBytes: 10, TotalCommittedBytes: 110, DedicatedBytes: 100, SharedBytes: 10, 0, 1),
            new(102, LocalBytes: 200, NonLocalBytes: 20, TotalCommittedBytes: 220, DedicatedBytes: 200, SharedBytes: 20, 0, 1),
            new(101, LocalBytes: 300, NonLocalBytes: 30, TotalCommittedBytes: 330, DedicatedBytes: 300, SharedBytes: 30, 0, 1)
        };

        var aggregated = GpuProcessMemoryAggregator.AggregateByPid(samples);

        Assert.Equal(2, aggregated.Count);

        var p101 = Assert.Single(aggregated, x => x.Pid == 101);
        Assert.Equal(400UL, p101.LocalBytes);
        Assert.Equal(40UL, p101.NonLocalBytes);

        var p102 = Assert.Single(aggregated, x => x.Pid == 102);
        Assert.Equal(200UL, p102.LocalBytes);
        Assert.Equal(20UL, p102.NonLocalBytes);
    }

    [Fact]
    public void AggregateByPid_EmptyInput_ReturnsEmptyList()
    {
        var aggregated = GpuProcessMemoryAggregator.AggregateByPid([]);
        Assert.Empty(aggregated);
    }
}
