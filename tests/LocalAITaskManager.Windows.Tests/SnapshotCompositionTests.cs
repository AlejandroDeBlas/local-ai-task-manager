using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Windows.Telemetry;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class SnapshotCompositionTests
{
    private sealed class FakeGpuProvider : IGpuTelemetryProvider
    {
        public List<GpuDeviceSnapshot> Gpus { get; set; } = [];

        public Task<IReadOnlyList<GpuDeviceSnapshot>> GetGpusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<GpuDeviceSnapshot>>(Gpus);
        }
    }

    private sealed class FakeProcessMemoryProvider : IGpuProcessMemoryProvider
    {
        public List<GpuProcessMemorySample> Samples { get; set; } = [];
        public bool ThrowException { get; set; }

        public Task<IReadOnlyList<GpuProcessMemorySample>> GetProcessMemoryAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowException)
            {
                throw new InvalidOperationException("Simulated PDH failure");
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
                    TotalVramBytes: 16UL * 1024 * 1024 * 1024,
                    UsedVramBytes: 4UL * 1024 * 1024 * 1024,
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
        Assert.Null(gpu.GpuUtilizationPercent);
        Assert.Null(gpu.TemperatureCelsius);
        Assert.Null(gpu.PowerWatts);
        Assert.Empty(snapshot.Warnings);
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
            ThrowException = true
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
    public async Task GetSnapshotAsync_ProcessDisappearing_HandledGracefully()
    {
        var gpuProvider = new FakeGpuProvider();
        var memProvider = new FakeProcessMemoryProvider
        {
            Samples =
            [
                // PID 99999999 does not exist on the system
                new GpuProcessMemorySample(99999999, 1024 * 1024 * 500, 0, 0, 1)
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
        Assert.Equal(0UL, proc.WorkingSetBytes); // Exited process yields 0 working set
    }
}
