using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Windows.Detection.LmStudio;
using LocalAITaskManager.Windows.Processes;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class LmStudioTests
{
    [Fact]
    public void LmStudioCliParser_ArrayFormat_ParsesCorrectly()
    {
        string json = @"
[
  {
    ""identifier"": ""mistral-small"",
    ""path"": ""C:\\Models\\mistral-small-Q4_K_M.gguf"",
    ""contextLength"": 8192,
    ""sizeBytes"": 14000000000
  }
]";

        var models = LmStudioCliParser.ParseModels(json);
        Assert.Single(models);
        Assert.Equal("mistral-small", models[0].Identifier);
        Assert.Equal(@"C:\Models\mistral-small-Q4_K_M.gguf", models[0].Path);
        Assert.Equal(8192, models[0].ContextLength);
        Assert.Equal(14000000000UL, models[0].SizeBytes);
    }

    [Fact]
    public void LmStudioCliParser_ObjectFormat_ParsesCorrectly()
    {
        string json = @"
{
  ""models"": [
    {
      ""modelKey"": ""qwen2.5-coder"",
      ""context_length"": 16384,
      ""size_bytes"": 9000000000
    }
  ]
}";

        var models = LmStudioCliParser.ParseModels(json);
        Assert.Single(models);
        Assert.Equal("qwen2.5-coder", models[0].ModelKey);
        Assert.Equal(16384, models[0].ContextLength);
        Assert.Equal(9000000000UL, models[0].SizeBytes);
    }

    [Fact]
    public void LmStudioCliParser_InvalidOrEmpty_ReturnsEmptyList()
    {
        Assert.Empty(LmStudioCliParser.ParseModels(""));
        Assert.Empty(LmStudioCliParser.ParseModels("invalid json"));
        Assert.Empty(LmStudioCliParser.ParseModels("{}"));
    }

    private sealed class MockCommandRunner : ILocalCommandRunner
    {
        private readonly CommandExecutionResult _result;

        public MockCommandRunner(CommandExecutionResult result)
        {
            _result = result;
        }

        public Task<CommandExecutionResult> ExecuteAsync(
            string executablePath,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }

    [Fact]
    public async Task LmStudioRuntimeDetector_OneModelOneRunner_CorrelatesUnambiguously()
    {
        string json = @"[{ ""identifier"": ""qwen2.5-14b"", ""path"": ""C:\\Models\\qwen-Q4_K_M.gguf"", ""contextLength"": 32768 }]";
        var mockRunner = new MockCommandRunner(new CommandExecutionResult(0, json, "", false));
        var detector = new LmStudioRuntimeDetector(mockRunner, cliPathResolver: () => @"C:\Mock\lms.exe");

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
        Assert.NotNull(proc.Model);
        Assert.Equal("qwen2.5-14b", proc.Model.DisplayName);
        Assert.Equal("Q4_K_M", proc.Model.Quantization);
        Assert.Equal(32768, proc.Model.ContextLength);
    }
}

