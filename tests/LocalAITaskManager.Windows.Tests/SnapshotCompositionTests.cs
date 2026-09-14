using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;
using LocalAITaskManager.Windows.PerformanceCounters;
using LocalAITaskManager.Windows.Telemetry;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class SnapshotCompositionTests
{
    private sealed class FakeGpuProvider : IGpuTelemetryProvider
    {
        public List<GpuDeviceSnapshot> Gpus { get; set; } = [];
        public bool ThrowCancellation { get; set; }

        public Task<IReadOnlyList<GpuDeviceSnapshot>> GetGpusAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowCancellation)
            {
                throw new OperationCanceledException();
            }
            return Task.FromResult<IReadOnlyList<GpuDeviceSnapshot>>(Gpus);
        }
    }

    private sealed class FakeProcessMemoryProvider : IGpuProcessMemoryProvider
    {
        public List<GpuProcessMemorySample> Samples { get; set; } = [];
        public bool ThrowPdhException { get; set; }
        public bool ThrowCancellation { get; set; }

        public Task<IReadOnlyList<GpuProcessMemorySample>> GetProcessMemoryAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowCancellation)
            {
                throw new OperationCanceledException();
            }
            if (ThrowPdhException)
            {
                throw new PdhTelemetryException("Simulated PDH collection error", 0xC0000BB8);
            }
            return Task.FromResult<IReadOnlyList<GpuProcessMemorySample>>(Samples);
        }
    }

    private sealed class FakeSystemMemoryProvider : ISystemMemoryProvider
    {
        public SystemMemorySnapshot Snapshot { get; set; } = new(32UL * 1024 * 1024 * 1024, 16UL * 1024 * 1024 * 1024, 16UL * 1024 * 1024 * 1024);

        public SystemMemorySnapshot GetSnapshot() => Snapshot;
    }

    private sealed class FakeProcessInfoProvider : IProcessInfoProvider
    {
        public Dictionary<int, ProcessMetadata> MetadataMap { get; } = [];
        public List<IReadOnlyCollection<int>> CleanupCalls { get; } = [];

        public ProcessMetadata GetMetadata(int pid)
        {
            if (MetadataMap.TryGetValue(pid, out var meta))
            {
                return meta;
            }
            return new ProcessMetadata(pid, $"Process_{pid}.exe", null, null);
        }

        public void Cleanup(IReadOnlyCollection<int> currentPids)
        {
            CleanupCalls.Add(currentPids);
        }
    }

    private sealed class FakeCpuSampler : IProcessCpuSampler
    {
        public double? CpuResult { get; set; } = 5.0;

        public double? SampleCpu(int pid, TimeSpan totalProcessorTime, DateTimeOffset timestamp) => CpuResult;

        public void Cleanup(IReadOnlyCollection<int> currentPids) { }
    }

    [Fact]
    public async Task GetSnapshotAsync_WithOptionalMetricsUnavailable_PopulatesNullsWithoutCrash()
    {
        var gpuProvider = new FakeGpuProvider
        {
            Gpus =
            [
                new GpuDeviceSnapshot(
                    Id: "gpu-0",
                    Index: 0,
                    Name: "NVIDIA GeForce RTX 4070 Ti SUPER",
                    TotalVramBytes: null,       // Failed to read memory info
                    UsedVramBytes: null,        // Failed to read memory info
                    GpuUtilizationPercent: null, // Unsupported
                    TemperatureCelsius: null,   // Unsupported
                    PowerWatts: null,           // Unsupported
                    DriverVersion: "560.94"
                )
            ]
        };

        var memProvider = new FakeProcessMemoryProvider();
        var sysMem = new FakeSystemMemoryProvider();
        var procInfo = new FakeProcessInfoProvider();
        var cpuSampler = new FakeCpuSampler();

        var snapshotProvider = new WindowsSystemSnapshotProvider(gpuProvider, memProvider, sysMem, procInfo, cpuSampler);

        var snapshot = await snapshotProvider.GetSnapshotAsync();

        Assert.Single(snapshot.Gpus);
        var gpu = snapshot.Gpus[0];
        Assert.Equal("NVIDIA GeForce RTX 4070 Ti SUPER", gpu.Name);
        Assert.Null(gpu.TotalVramBytes);
        Assert.Null(gpu.UsedVramBytes);
        Assert.Null(gpu.GpuUtilizationPercent);
        Assert.Null(gpu.TemperatureCelsius);
        Assert.Null(gpu.PowerWatts);

        // Verify ByteFormatter handles null VRAM without displaying "0 / 0 GB"
        string vramFormatted = ByteFormatter.FormatRatio(gpu.UsedVramBytes, gpu.TotalVramBytes);
        Assert.Equal("—", vramFormatted);
    }

    [Fact]
    public async Task GetSnapshotAsync_PdhFailure_EmitsWarningAndPreservesGpuSnapshot()
    {
        var gpuProvider = new FakeGpuProvider
        {
            Gpus =
            [
                new GpuDeviceSnapshot("gpu-0", 0, "RTX 4070", 16000, 4000, 50, 60, 200, "560.94")
            ]
        };

        var memProvider = new FakeProcessMemoryProvider
        {
            ThrowPdhException = true
        };

        var sysMem = new FakeSystemMemoryProvider();
        var procInfo = new FakeProcessInfoProvider();
        var cpuSampler = new FakeCpuSampler();

        var snapshotProvider = new WindowsSystemSnapshotProvider(gpuProvider, memProvider, sysMem, procInfo, cpuSampler);

        var snapshot = await snapshotProvider.GetSnapshotAsync();

        // GPU must still be present
        Assert.Single(snapshot.Gpus);
        // Process list is empty due to PDH failure
        Assert.Empty(snapshot.GpuProcesses);
        // Warning must be generated for PDH
        var warning = Assert.Single(snapshot.Warnings);
        Assert.Equal("PDH", warning.Source);
        Assert.Contains("GPU process telemetry unavailable", warning.Message);
    }

    [Fact]
    public async Task GetSnapshotAsync_ValidEmptyPdhResult_ProducesEmptyListWithoutWarning()
    {
        var gpuProvider = new FakeGpuProvider
        {
            Gpus =
            [
                new GpuDeviceSnapshot("gpu-0", 0, "RTX 4070", 16000, 4000, 50, 60, 200, "560.94")
            ]
        };

        var memProvider = new FakeProcessMemoryProvider
        {
            Samples = [] // Valid empty result (0 processes)
        };

        var sysMem = new FakeSystemMemoryProvider();
        var procInfo = new FakeProcessInfoProvider();
        var cpuSampler = new FakeCpuSampler();

        var snapshotProvider = new WindowsSystemSnapshotProvider(gpuProvider, memProvider, sysMem, procInfo, cpuSampler);

        var snapshot = await snapshotProvider.GetSnapshotAsync();

        Assert.Single(snapshot.Gpus);
        Assert.Empty(snapshot.GpuProcesses);
        Assert.Empty(snapshot.Warnings); // No warning when PDH simply observed 0 processes!
    }

    [Fact]
    public async Task GetSnapshotAsync_ProcessDisappearing_YieldsNullWorkingSet()
    {
        var gpuProvider = new FakeGpuProvider();
        var memProvider = new FakeProcessMemoryProvider
        {
            Samples =
            [
                // PID 99999999 does not exist on the system
                new GpuProcessMemorySample(99999999, 1024 * 1024 * 500, 0, 0, 0, 0, 0, 1)
            ]
        };

        var sysMem = new FakeSystemMemoryProvider();
        var procInfo = new FakeProcessInfoProvider();
        var cpuSampler = new FakeCpuSampler();

        var snapshotProvider = new WindowsSystemSnapshotProvider(gpuProvider, memProvider, sysMem, procInfo, cpuSampler);

        var snapshot = await snapshotProvider.GetSnapshotAsync();

        Assert.Single(snapshot.GpuProcesses);
        var proc = snapshot.GpuProcesses[0];
        Assert.Equal(99999999, proc.Pid);
        Assert.Null(proc.WorkingSetBytes); // Exited process yields null working set, not 0 B

        string ramText = ByteFormatter.Format(proc.WorkingSetBytes, "—");
        Assert.Equal("—", ramText); // Formatted as "—" rather than "0 B"
    }

    [Fact]
    public async Task GetSnapshotAsync_SystemMemoryUnavailable_EmitsWarningAndProvidesNulls()
    {
        var gpuProvider = new FakeGpuProvider();
        var memProvider = new FakeProcessMemoryProvider();
        var sysMem = new FakeSystemMemoryProvider
        {
            Snapshot = new SystemMemorySnapshot(null, null, null)
        };
        var procInfo = new FakeProcessInfoProvider();
        var cpuSampler = new FakeCpuSampler();

        var snapshotProvider = new WindowsSystemSnapshotProvider(gpuProvider, memProvider, sysMem, procInfo, cpuSampler);

        var snapshot = await snapshotProvider.GetSnapshotAsync();

        Assert.Null(snapshot.Memory.TotalPhysicalBytes);
        Assert.Null(snapshot.Memory.UsedPhysicalBytes);
        var warn = Assert.Single(snapshot.Warnings, w => w.Source == "Memory");
        Assert.Contains("System memory query unavailable", warn.Message);

        string formatted = ByteFormatter.FormatRatio(snapshot.Memory.UsedPhysicalBytes, snapshot.Memory.TotalPhysicalBytes);
        Assert.Equal("—", formatted);
    }

    [Fact]
    public async Task GetSnapshotAsync_OperationCanceledException_PropagatesWithoutWarning()
    {
        var gpuProvider = new FakeGpuProvider { ThrowCancellation = true };
        var memProvider = new FakeProcessMemoryProvider();
        var sysMem = new FakeSystemMemoryProvider();
        var procInfo = new FakeProcessInfoProvider();
        var cpuSampler = new FakeCpuSampler();

        var snapshotProvider = new WindowsSystemSnapshotProvider(gpuProvider, memProvider, sysMem, procInfo, cpuSampler);

        await Assert.ThrowsAsync<OperationCanceledException>(() => snapshotProvider.GetSnapshotAsync());
    }
}
