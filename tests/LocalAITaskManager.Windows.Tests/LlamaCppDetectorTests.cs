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
        Assert.Equal(DetectionConfidence.High, id.RuntimeConfidence);
        Assert.Null(id.Model);
        Assert.Equal(DetectionConfidence.None, id.ModelConfidence);
    }

    [Fact]
    public async Task DetectAsync_ExecutableOnly_RuntimeConfidenceMediumAndModelNull()
    {
        var detector = new LlamaCppRuntimeDetector(_parser);

        var proc = new GpuProcessSnapshot(
            Pid: 25143,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 1000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 1000000,
            DedicatedGpuMemoryBytes: 1000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Tools\llama-server.exe",
            CommandLine: ""
        );

        var snapshot = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(snapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        var id = result.IdentifiedProcesses[0];
        Assert.Equal(AiRuntimeKind.LlamaCpp, id.Runtime);
        Assert.Equal(DetectionConfidence.Medium, id.RuntimeConfidence);
        Assert.Null(id.Model);
        Assert.Equal(DetectionConfidence.None, id.ModelConfidence);
    }

    [Fact]
    public async Task DetectAsync_GenericFlagsOnly_RuntimeConfidenceMediumAndModelNull()
    {
        var detector = new LlamaCppRuntimeDetector(_parser);

        var proc = new GpuProcessSnapshot(
            Pid: 25144,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 1000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 1000000,
            DedicatedGpuMemoryBytes: 1000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Tools\llama-server.exe",
            CommandLine: @"llama-server.exe --help --threads 8 --port 8080"
        );

        var snapshot = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(snapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        var id = result.IdentifiedProcesses[0];
        Assert.Equal(AiRuntimeKind.LlamaCpp, id.Runtime);
        Assert.Equal(DetectionConfidence.Medium, id.RuntimeConfidence);
        Assert.Null(id.Model);
        Assert.Equal(DetectionConfidence.None, id.ModelConfidence);
    }

    [Fact]
    public async Task DetectAsync_MalformedArguments_DoesNotElevateConfidence()
    {
        var detector = new LlamaCppRuntimeDetector(_parser);

        // Case 1: -m without value
        var proc1 = new GpuProcessSnapshot(
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

        var snapshot1 = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc1], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context1 = new WorkloadDetectionContext(snapshot1, relationships);

        var result1 = await detector.DetectAsync(context1, CancellationToken.None);
        Assert.Single(result1.IdentifiedProcesses);
        Assert.Equal(DetectionConfidence.Medium, result1.IdentifiedProcesses[0].RuntimeConfidence);
        Assert.Null(result1.IdentifiedProcesses[0].Model);
        Assert.Equal(25142, result1.IdentifiedProcesses[0].RuntimeRootPid);

        // Case 2: -c abc (invalid integer)
        var proc2 = new GpuProcessSnapshot(
            Pid: 25145,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: null,
            NonLocalGpuMemoryBytes: null,
            TotalCommittedGpuMemoryBytes: null,
            DedicatedGpuMemoryBytes: null,
            SharedGpuMemoryBytes: null,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: null,
            CommandLine: "llama-server.exe -c abc"
        );
        var snapshot2 = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc2], []);
        var context2 = new WorkloadDetectionContext(snapshot2, relationships);
        var result2 = await detector.DetectAsync(context2, CancellationToken.None);
        Assert.Single(result2.IdentifiedProcesses);
        Assert.Equal(DetectionConfidence.Medium, result2.IdentifiedProcesses[0].RuntimeConfidence);

        // Case 3: --model= (empty value)
        var proc3 = new GpuProcessSnapshot(
            Pid: 25146,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: null,
            NonLocalGpuMemoryBytes: null,
            TotalCommittedGpuMemoryBytes: null,
            DedicatedGpuMemoryBytes: null,
            SharedGpuMemoryBytes: null,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: null,
            CommandLine: "llama-server.exe --model="
        );
        var snapshot3 = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc3], []);
        var context3 = new WorkloadDetectionContext(snapshot3, relationships);
        var result3 = await detector.DetectAsync(context3, CancellationToken.None);
        Assert.Single(result3.IdentifiedProcesses);
        Assert.Equal(DetectionConfidence.Medium, result3.IdentifiedProcesses[0].RuntimeConfidence);

        // Case 4: --models-dir= (empty value)
        var proc4 = new GpuProcessSnapshot(
            Pid: 25147,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: null,
            NonLocalGpuMemoryBytes: null,
            TotalCommittedGpuMemoryBytes: null,
            DedicatedGpuMemoryBytes: null,
            SharedGpuMemoryBytes: null,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: null,
            CommandLine: "llama-server.exe --models-dir="
        );
        var snapshot4 = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [proc4], []);
        var context4 = new WorkloadDetectionContext(snapshot4, relationships);
        var result4 = await detector.DetectAsync(context4, CancellationToken.None);
        Assert.Single(result4.IdentifiedProcesses);
        Assert.Equal(DetectionConfidence.Medium, result4.IdentifiedProcesses[0].RuntimeConfidence);
    }
}
