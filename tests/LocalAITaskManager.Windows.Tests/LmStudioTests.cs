using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Windows.Detection.LmStudio;
using LocalAITaskManager.Windows.Processes;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class LmStudioTests
{
    [Fact]
    public async Task LmStudioRuntimeDetector_ChildProcess_IdentifiedWithMediumConfidenceAndModelNull()
    {
        var detector = new LmStudioRuntimeDetector();

        var runner = new GpuProcessSnapshot(
            Pid: 5000,
            ProcessName: "LM Studio child.exe",
            LocalGpuMemoryBytes: 10000000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 10000000000,
            DedicatedGpuMemoryBytes: 10000000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\LM Studio\child.exe",
            CommandLine: ""
        );

        var telemetrySnapshot = new SystemSnapshot(DateTimeOffset.UtcNow,
            Gpus: [],
            Memory: new SystemMemorySnapshot(null, null, null),
            GpuProcesses: [runner],
            Warnings: []
        );

        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(telemetrySnapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        var proc = result.IdentifiedProcesses[0];
        Assert.Equal(5000, proc.Pid);
        Assert.Equal(AiRuntimeKind.LmStudio, proc.Runtime);
        Assert.Equal(DetectionConfidence.Medium, proc.RuntimeConfidence);
        Assert.Null(proc.Model);
        Assert.Equal(DetectionConfidence.None, proc.ModelConfidence);
        Assert.Null(proc.RuntimeRootPid);
        Assert.Empty(result.UnmappedModels);
    }

    [Fact]
    public async Task LmStudioRuntimeDetector_DirectExecutable_IdentifiedWithConfirmedConfidenceAndRootPid()
    {
        var detector = new LmStudioRuntimeDetector();

        var proc = new GpuProcessSnapshot(
            Pid: 5001,
            ProcessName: "LM Studio.exe",
            LocalGpuMemoryBytes: 2000000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 2000000000,
            DedicatedGpuMemoryBytes: 2000000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\LM Studio\LM Studio.exe",
            CommandLine: ""
        );

        var telemetrySnapshot = new SystemSnapshot(DateTimeOffset.UtcNow,
            Gpus: [],
            Memory: new SystemMemorySnapshot(null, null, null),
            GpuProcesses: [proc],
            Warnings: []
        );

        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(telemetrySnapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        var identified = result.IdentifiedProcesses[0];
        Assert.Equal(5001, identified.Pid);
        Assert.Equal(AiRuntimeKind.LmStudio, identified.Runtime);
        Assert.Equal(DetectionConfidence.Confirmed, identified.RuntimeConfidence);
        Assert.Null(identified.Model);
        Assert.Equal(DetectionConfidence.None, identified.ModelConfidence);
        Assert.Equal(5001, identified.RuntimeRootPid);
    }

    [Fact]
    public async Task LmStudioRuntimeDetector_DescendantProcess_CapturesRuntimeRootPid()
    {
        var detector = new LmStudioRuntimeDetector();

        var child = new GpuProcessSnapshot(
            Pid: 5002,
            ProcessName: "worker.exe",
            LocalGpuMemoryBytes: 4000000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 4000000000,
            DedicatedGpuMemoryBytes: 4000000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\LM Studio\worker.exe",
            CommandLine: ""
        );

        var telemetrySnapshot = new SystemSnapshot(DateTimeOffset.UtcNow,
            Gpus: [],
            Memory: new SystemMemorySnapshot(null, null, null),
            GpuProcesses: [child],
            Warnings: []
        );

        // Map child PID 5002 -> parent PID 5001 (which is LM Studio root)
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int> { [5002] = 5001 });
        var context = new WorkloadDetectionContext(telemetrySnapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        var identified = result.IdentifiedProcesses[0];
        Assert.Equal(5002, identified.Pid);
        Assert.Equal(AiRuntimeKind.LmStudio, identified.Runtime);
        Assert.Equal(DetectionConfidence.Medium, identified.RuntimeConfidence);
    }
}

