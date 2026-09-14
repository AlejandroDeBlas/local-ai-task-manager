using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Windows.Detection;
using LocalAITaskManager.Windows.Processes;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class WorkloadDetectionCoordinatorTests
{
    private sealed class FakeRelationshipProvider : IProcessRelationshipProvider
    {
        private readonly IProcessRelationshipSnapshot _snapshot;
        public FakeRelationshipProvider(IProcessRelationshipSnapshot snapshot) => _snapshot = snapshot;
        public IProcessRelationshipSnapshot GetSnapshot() => _snapshot;
    }

    private sealed class FakeDetector : IRuntimeDetector
    {
        public AiRuntimeKind Runtime { get; }
        private readonly RuntimeDetectionResult _result;

        public FakeDetector(AiRuntimeKind runtime, RuntimeDetectionResult result)
        {
            Runtime = runtime;
            _result = result;
        }

        public Task<RuntimeDetectionResult> DetectAsync(WorkloadDetectionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }

    [Fact]
    public async Task DetectWorkloadsAsync_OllamaOverridesLlamaCppFallback()
    {
        int pid = 46588;

        var llamaResult = new RuntimeDetectionResult(
            IdentifiedProcesses: [
                new AiProcessIdentity(
                    pid,
                    AiRuntimeKind.LlamaCpp,
                    DetectionConfidence.High,
                    null,
                    DetectionConfidence.None,
                    [new(DetectionEvidenceKind.ExecutableName, "llama-server.exe")]
                )
            ],
            UnmappedModels: [],
            Warnings: []
        );

        var ollamaResult = new RuntimeDetectionResult(
            IdentifiedProcesses: [
                new AiProcessIdentity(
                    pid,
                    AiRuntimeKind.Ollama,
                    DetectionConfidence.Confirmed,
                    new DetectedModelIdentity("qwen2.5:1.5b", null, "Q4_K_M", null, "1.5B", 32768),
                    DetectionConfidence.Confirmed,
                    [new(DetectionEvidenceKind.ProcessAncestry, "Ollama ancestor")]
                )
            ],
            UnmappedModels: [],
            Warnings: []
        );

        var detectors = new IRuntimeDetector[]
        {
            new FakeDetector(AiRuntimeKind.LlamaCpp, llamaResult),
            new FakeDetector(AiRuntimeKind.Ollama, ollamaResult)
        };

        var relSnapshot = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var coordinator = new WorkloadDetectionCoordinator(new FakeRelationshipProvider(relSnapshot), detectors);

        var systemSnapshot = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [], []);
        var detection = await coordinator.DetectWorkloadsAsync(systemSnapshot, CancellationToken.None);

        Assert.True(detection.Processes.TryGetValue(pid, out var resolved));
        Assert.Equal(AiRuntimeKind.Ollama, resolved.Runtime);
        Assert.Equal(DetectionConfidence.Confirmed, resolved.RuntimeConfidence);
        Assert.Equal("qwen2.5:1.5b", resolved.Model?.DisplayName);
    }

    [Fact]
    public async Task DetectWorkloadsAsync_UnknownProcess_RemainsUnknown()
    {
        var proc = new GpuProcessSnapshot(
            Pid: 12345,
            ProcessName: "python.exe",
            LocalGpuMemoryBytes: 8000000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 8000000000,
            DedicatedGpuMemoryBytes: 8000000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: 4000000000,
            CpuPercent: 5.0,
            ExecutablePath: @"C:\Python312\python.exe",
            CommandLine: @"python.exe train.py"
        );

        var emptyResult = new RuntimeDetectionResult([], [], []);
        var detectors = new IRuntimeDetector[]
        {
            new FakeDetector(AiRuntimeKind.LlamaCpp, emptyResult),
            new FakeDetector(AiRuntimeKind.Ollama, emptyResult)
        };

        var relSnapshot = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var coordinator = new WorkloadDetectionCoordinator(new FakeRelationshipProvider(relSnapshot), detectors);

        var systemSnapshot = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc], []);
        var detection = await coordinator.DetectWorkloadsAsync(systemSnapshot, CancellationToken.None);

        // UNKNOWN > WRONG: python.exe must NOT be mapped to any AI workload
        Assert.False(detection.Processes.ContainsKey(12345));
    }
}
