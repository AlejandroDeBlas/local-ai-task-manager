using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Windows.Detection.LlamaCpp;
using LocalAITaskManager.Windows.Processes;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class LlamaCppDetectorTests
{
    private readonly WindowsCommandLineParser _parser = new();

    [Fact]
    public async Task DetectAsync_StandardServerWithModel_IdentifiesModelAndContext()
    {
        var detector = new LlamaCppRuntimeDetector(_parser);

        var proc = new GpuProcessSnapshot(
            Pid: 25140,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 8000000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 8000000000,
            DedicatedGpuMemoryBytes: 8000000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: 5000000000,
            CpuPercent: 2.0,
            ExecutablePath: @"C:\Tools\llama-server.exe",
            CommandLine: @"llama-server.exe -m ""C:\Models\Qwen2.5-Coder-14B-Q4_K_M.gguf"" -c 32768"
        );

        var snapshot = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(snapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        var id = result.IdentifiedProcesses[0];
        Assert.Equal(25140, id.Pid);
        Assert.Equal(AiRuntimeKind.LlamaCpp, id.Runtime);
        Assert.Equal(DetectionConfidence.High, id.RuntimeConfidence);
        Assert.NotNull(id.Model);
        Assert.Equal("Qwen2.5-Coder-14B-Q4_K_M", id.Model.DisplayName);
        Assert.Equal("Q4_K_M", id.Model.Quantization);
        Assert.Equal(32768, id.Model.ContextLength);
    }

    [Fact]
    public async Task DetectAsync_ModelsDirWithoutDirectModel_RuntimeLlamaCppWithModelNull()
    {
        var detector = new LlamaCppRuntimeDetector(_parser);

        var proc = new GpuProcessSnapshot(
            Pid: 25141,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 8000000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 8000000000,
            DedicatedGpuMemoryBytes: 8000000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Tools\llama-server.exe",
            CommandLine: @"llama-server.exe --models-dir C:\Models"
        );

        var snapshot = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(snapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        var id = result.IdentifiedProcesses[0];
        Assert.Equal(AiRuntimeKind.LlamaCpp, id.Runtime);
        Assert.Null(id.Model);
    }

    [Fact]
    public async Task DetectAsync_MalformedArguments_DoesNotCrash()
    {
        var detector = new LlamaCppRuntimeDetector(_parser);

        var proc = new GpuProcessSnapshot(
            Pid: 25142,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: null,
            NonLocalGpuMemoryBytes: null,
            TotalCommittedGpuMemoryBytes: null,
            DedicatedGpuMemoryBytes: null,
            SharedGpuMemoryBytes: null,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: null,
            CommandLine: "llama-server.exe -m"
        );

        var snapshot = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(snapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        Assert.Null(result.IdentifiedProcesses[0].Model);
    }
}
