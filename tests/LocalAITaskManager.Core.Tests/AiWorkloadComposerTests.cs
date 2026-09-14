using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;
using Xunit;

namespace LocalAITaskManager.Core.Tests;

public class AiWorkloadComposerTests
{
    private readonly AiWorkloadComposer _composer = new();

    private static GpuProcessSnapshot CreateProcess(int pid, string name, ulong? localGpu = 1000000000, ulong? ram = 500000000, double? cpu = 2.5)
    {
        return new GpuProcessSnapshot(
            Pid: pid,
            ProcessName: name,
            LocalGpuMemoryBytes: localGpu,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: localGpu,
            DedicatedGpuMemoryBytes: localGpu,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: ram,
            CpuPercent: cpu,
            ExecutablePath: $@"C:\Tools\{name}",
            CommandLine: name
        );
    }

    private static SystemSnapshot CreateSnapshot(params GpuProcessSnapshot[] procs)
    {
        return new SystemSnapshot(
            Timestamp: DateTimeOffset.UtcNow,
            Gpus: [],
            Memory: new SystemMemorySnapshot(null, null, null),
            GpuProcesses: procs,
            Warnings: []
        );
    }

    private static DetectionSnapshot CreateDetection(params AiProcessIdentity[] identities)
    {
        var dict = identities.ToDictionary(i => i.Pid);
        return new DetectionSnapshot(
            Timestamp: DateTimeOffset.UtcNow,
            Processes: dict,
            UnmappedModels: [],
            Warnings: []
        );
    }

    [Fact]
    public void TestA_StandaloneLlamaCpp_ProducesSingleModelWorkload()
    {
        var proc = CreateProcess(100, "llama-server.exe", localGpu: 5000000000);
        var model = new DetectedModelIdentity("Qwen2.5-14B", "C:\\m.gguf", "Q4_K_M", null, null, 32768);
        var identity = new AiProcessIdentity(
            Pid: 100,
            Runtime: AiRuntimeKind.LlamaCpp,
            RuntimeConfidence: DetectionConfidence.High,
            Model: model,
            ModelConfidence: DetectionConfidence.High,
            Evidence: [new(DetectionEvidenceKind.CommandLine, "-m arg")],
            RuntimeRootPid: 100
        );

        var result = _composer.Compose(CreateSnapshot(proc), CreateDetection(identity));

        Assert.Single(result);
        var w = result[0];
        Assert.Equal(AiWorkloadKind.Model, w.Kind);
        Assert.Equal(AiRuntimeKind.LlamaCpp, w.Runtime);
        Assert.Equal(100, w.PrimaryPid);
        Assert.Equal(100, w.RuntimeRootPid);
        Assert.Equal("Qwen2.5-14B", w.DisplayName);
        Assert.Equal([100], w.ProcessPids);
        Assert.Equal(5000000000UL, w.PrimaryLocalGpuMemoryBytes);
        Assert.Equal("llamacpp:model:100", w.WorkloadId);
    }

    [Fact]
    public void TestB_OllamaOneModel_GroupsControllerAndRunnerWithoutDuplication()
    {
        var root = CreateProcess(100, "ollama.exe", localGpu: 15000000);
        var runner = CreateProcess(200, "llama-server.exe", localGpu: 1300000000);

        var model = new DetectedModelIdentity("qwen2.5:1.5b", null, "Q4_K_M", null, "1.5B", 4096);
        var rootIdentity = new AiProcessIdentity(100, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 100);
        var runnerIdentity = new AiProcessIdentity(200, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, model, DetectionConfidence.Confirmed, [], 100);

        var result = _composer.Compose(CreateSnapshot(root, runner), CreateDetection(rootIdentity, runnerIdentity));

        Assert.Single(result);
        var w = result[0];
        Assert.Equal(AiWorkloadKind.Model, w.Kind);
        Assert.Equal("qwen2.5:1.5b", w.DisplayName);
        Assert.Equal(200, w.PrimaryPid);
        Assert.Equal(100, w.RuntimeRootPid);
        Assert.Equal(1300000000UL, w.PrimaryLocalGpuMemoryBytes);
        Assert.Equal(2, w.ProcessPids.Count);
        Assert.Contains(100, w.ProcessPids);
        Assert.Contains(200, w.ProcessPids);
        Assert.Equal(w.ProcessPids.Distinct().Count(), w.ProcessPids.Count);
    }

    [Fact]
    public void TestC_OllamaTwoModels_SeparateModelWorkloadsAndSharedRuntimeService()
    {
        var root = CreateProcess(100, "ollama.exe", localGpu: 12000000);
        var runner1 = CreateProcess(200, "llama-server.exe", localGpu: 5000000000);
        var runner2 = CreateProcess(300, "llama-server.exe", localGpu: 3000000000);

        var qwenModel = new DetectedModelIdentity("qwen2.5:14b", null, "Q4_K_M", null, "14B", 32768);
        var llamaModel = new DetectedModelIdentity("llama3.2:3b", null, "Q4_K_M", null, "3B", 8192);

        var rootId = new AiProcessIdentity(100, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 100);
        var r1Id = new AiProcessIdentity(200, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, qwenModel, DetectionConfidence.Confirmed, [], 100);
        var r2Id = new AiProcessIdentity(300, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, llamaModel, DetectionConfidence.Confirmed, [], 100);

        var result = _composer.Compose(CreateSnapshot(root, runner1, runner2), CreateDetection(rootId, r1Id, r2Id));

        Assert.Equal(3, result.Count);
        var qwenWorkload = result.First(w => w.DisplayName == "qwen2.5:14b");
        var llamaWorkload = result.First(w => w.DisplayName == "llama3.2:3b");
        var serviceWorkload = result.First(w => w.Kind == AiWorkloadKind.RuntimeService);

        Assert.Equal(AiWorkloadKind.Model, qwenWorkload.Kind);
        Assert.Equal([200], qwenWorkload.ProcessPids);
        Assert.Equal(200, qwenWorkload.PrimaryPid);

        Assert.Equal(AiWorkloadKind.Model, llamaWorkload.Kind);
        Assert.Equal([300], llamaWorkload.ProcessPids);
        Assert.Equal(300, llamaWorkload.PrimaryPid);

        Assert.Equal([100], serviceWorkload.ProcessPids);
        Assert.Equal(100, serviceWorkload.PrimaryPid);
        Assert.Equal("Ollama runtime services", serviceWorkload.DisplayName);

        // Verify no duplicated PIDs across all workloads
        var allPids = result.SelectMany(w => w.ProcessPids).ToList();
        Assert.Equal(allPids.Distinct().Count(), allPids.Count);
    }

    [Fact]
    public void TestD_OllamaNoModel_ProducesRuntimeOnlyWorkload()
    {
        var root = CreateProcess(100, "ollama.exe", localGpu: 10000000);
        var helper = CreateProcess(101, "ollama app.exe", localGpu: 0);

        var id1 = new AiProcessIdentity(100, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 100);
        var id2 = new AiProcessIdentity(101, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 100);

        var result = _composer.Compose(CreateSnapshot(root, helper), CreateDetection(id1, id2));

        Assert.Single(result);
        var w = result[0];
        Assert.Equal(AiWorkloadKind.RuntimeOnly, w.Kind);
        Assert.Equal("Ollama", w.DisplayName);
        Assert.Equal(100, w.PrimaryPid);
        Assert.Equal(2, w.ProcessPids.Count);
    }

    [Fact]
    public void TestE_LmStudioRuntime_GroupsAsRuntimeOnlyWithModelNull()
    {
        var root = CreateProcess(400, "LM Studio.exe", localGpu: 50000000);
        var child1 = CreateProcess(401, "child.exe", localGpu: 2000000000);
        var child2 = CreateProcess(402, "child.exe", localGpu: 1000000000);

        var idRoot = new AiProcessIdentity(400, AiRuntimeKind.LmStudio, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 400);
        var idC1 = new AiProcessIdentity(401, AiRuntimeKind.LmStudio, DetectionConfidence.Medium, null, DetectionConfidence.None, [], 400);
        var idC2 = new AiProcessIdentity(402, AiRuntimeKind.LmStudio, DetectionConfidence.Medium, null, DetectionConfidence.None, [], 400);

        var result = _composer.Compose(CreateSnapshot(root, child1, child2), CreateDetection(idRoot, idC1, idC2));

        Assert.Single(result);
        var w = result[0];
        Assert.Equal(AiWorkloadKind.RuntimeOnly, w.Kind);
        Assert.Equal(AiRuntimeKind.LmStudio, w.Runtime);
        Assert.Equal("LM Studio", w.DisplayName);
        Assert.Null(w.Model);
        Assert.Equal(400, w.PrimaryPid); // root has GPU consumption > 0
        Assert.Equal(3, w.ProcessPids.Count);
    }

    [Fact]
    public void TestF_DifferentLmStudioRoots_DoNotMerge()
    {
        var root1 = CreateProcess(400, "LM Studio.exe", localGpu: 1000000);
        var root2 = CreateProcess(500, "LM Studio.exe", localGpu: 2000000);

        var id1 = new AiProcessIdentity(400, AiRuntimeKind.LmStudio, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 400);
        var id2 = new AiProcessIdentity(500, AiRuntimeKind.LmStudio, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 500);

        var result = _composer.Compose(CreateSnapshot(root1, root2), CreateDetection(id1, id2));

        Assert.Equal(2, result.Count);
        Assert.NotEqual(result[0].WorkloadId, result[1].WorkloadId);
    }

    [Fact]
    public void TestG_UnknownGpuProcesses_ProduceZeroWorkloads()
    {
        var dwm = CreateProcess(1276, "dwm.exe", localGpu: 300000000);
        var firefox = CreateProcess(12000, "firefox.exe", localGpu: 150000000);

        var result = _composer.Compose(CreateSnapshot(dwm, firefox), CreateDetection());

        Assert.Empty(result);
    }

    [Fact]
    public void TestH_Invariant_ProcessPidsAreUniqueAcrossAllWorkloads()
    {
        var root = CreateProcess(100, "ollama.exe");
        var r1 = CreateProcess(200, "llama-server.exe");
        var r2 = CreateProcess(300, "llama-server.exe");
        var lmRoot = CreateProcess(400, "LM Studio.exe");
        var lmChild = CreateProcess(401, "child.exe");
        var llama = CreateProcess(500, "llama-server.exe");

        var m1 = new DetectedModelIdentity("m1", null, null, null, null, null);
        var m2 = new DetectedModelIdentity("m2", null, null, null, null, null);
        var m3 = new DetectedModelIdentity("m3", null, null, null, null, null);

        var ids = new[]
        {
            new AiProcessIdentity(100, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 100),
            new AiProcessIdentity(200, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, m1, DetectionConfidence.Confirmed, [], 100),
            new AiProcessIdentity(300, AiRuntimeKind.Ollama, DetectionConfidence.Confirmed, m2, DetectionConfidence.Confirmed, [], 100),
            new AiProcessIdentity(400, AiRuntimeKind.LmStudio, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 400),
            new AiProcessIdentity(401, AiRuntimeKind.LmStudio, DetectionConfidence.Medium, null, DetectionConfidence.None, [], 400),
            new AiProcessIdentity(500, AiRuntimeKind.LlamaCpp, DetectionConfidence.High, m3, DetectionConfidence.High, [], 500)
        };

        var result = _composer.Compose(CreateSnapshot(root, r1, r2, lmRoot, lmChild, llama), CreateDetection(ids));

        var allPids = result.SelectMany(w => w.ProcessPids).ToList();
        Assert.Equal(allPids.Distinct().Count(), allPids.Count);
    }

    [Fact]
    public void TestI_DeterministicWorkloadId()
    {
        var proc = CreateProcess(100, "llama-server.exe");
        var model = new DetectedModelIdentity("Qwen", null, null, null, null, null);
        var id = new AiProcessIdentity(100, AiRuntimeKind.LlamaCpp, DetectionConfidence.High, model, DetectionConfidence.High, [], 100);

        var result1 = _composer.Compose(CreateSnapshot(proc), CreateDetection(id));
        var result2 = _composer.Compose(CreateSnapshot(proc), CreateDetection(id));

        Assert.Equal(result1[0].WorkloadId, result2[0].WorkloadId);
    }

    [Fact]
    public void TestJ_MissingMetrics_PreservedAsNullNotZero()
    {
        var proc = CreateProcess(100, "llama-server.exe", localGpu: null, ram: null, cpu: null);
        var model = new DetectedModelIdentity("Qwen", null, null, null, null, null);
        var id = new AiProcessIdentity(100, AiRuntimeKind.LlamaCpp, DetectionConfidence.High, model, DetectionConfidence.High, [], 100);

        var result = _composer.Compose(CreateSnapshot(proc), CreateDetection(id));

        Assert.Single(result);
        var w = result[0];
        Assert.Null(w.PrimaryLocalGpuMemoryBytes);
        Assert.Null(w.PrimaryWorkingSetBytes);
        Assert.Null(w.PrimaryCpuPercent);
    }
}
