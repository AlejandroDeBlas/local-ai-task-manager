using System.Net;
using System.Text;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Windows.Detection.Ollama;
using LocalAITaskManager.Windows.Processes;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class OllamaTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task OllamaApiClient_ValidResponse_DeserializesCorrectly()
    {
        string json = @"
{
  ""models"": [
    {
      ""name"": ""qwen2.5:1.5b"",
      ""model"": ""qwen2.5:1.5b"",
      ""size"": 986000000,
      ""size_vram"": 900000000,
      ""context_length"": 32768,
      ""details"": {
        ""format"": ""gguf"",
        ""family"": ""qwen2"",
        ""parameter_size"": ""1.5B"",
        ""quantization_level"": ""Q4_K_M""
      }
    }
  ]
}";

        using var handler = new MockHttpMessageHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaApiClient(httpClient);

        var result = await client.GetLoadedModelsAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Models);
        Assert.Single(result.Models);
        var model = result.Models[0];
        Assert.Equal("qwen2.5:1.5b", model.Name);
        Assert.Equal(900000000UL, model.SizeVram);
        Assert.Equal(32768, model.ContextLength);
        Assert.NotNull(model.Details);
        Assert.Equal("Q4_K_M", model.Details.QuantizationLevel);
        Assert.Equal("1.5B", model.Details.ParameterSize);
    }

    [Fact]
    public async Task OllamaApiClient_EmptyModels_ReturnsEmptyList()
    {
        string json = @"{ ""models"": [] }";
        using var handler = new MockHttpMessageHandler(HttpStatusCode.OK, json);
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaApiClient(httpClient);

        var result = await client.GetLoadedModelsAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotNull(result.Models);
        Assert.Empty(result.Models);
    }

    [Fact]
    public async Task OllamaApiClient_HttpError_ReturnsNull()
    {
        using var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "Error");
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaApiClient(httpClient);

        var result = await client.GetLoadedModelsAsync(CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task OllamaApiClient_MalformedJson_ReturnsNull()
    {
        using var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "not-json");
        using var httpClient = new HttpClient(handler);
        using var client = new OllamaApiClient(httpClient);

        var result = await client.GetLoadedModelsAsync(CancellationToken.None);
        Assert.Null(result);
    }

    private sealed class FakeOllamaApiClient : IOllamaApiClient
    {
        private readonly OllamaPsResponse? _response;

        public FakeOllamaApiClient(OllamaPsResponse? response)
        {
            _response = response;
        }

        public Task<OllamaPsResponse?> GetLoadedModelsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    [Fact]
    public async Task OllamaRuntimeDetector_OneModelOneRunner_CorrelatesUnambiguously()
    {
        var psResponse = new OllamaPsResponse
        {
            Models =
            [
                new()
                {
                    Name = "qwen2.5:1.5b",
                    SizeVram = 1300000000UL,
                    ContextLength = 32768,
                    Details = new()
                    {
                        Family = "qwen2",
                        ParameterSize = "1.5B",
                        QuantizationLevel = "Q4_K_M"
                    }
                }
            ]
        };

        var fakeClient = new FakeOllamaApiClient(psResponse);
        var detector = new OllamaRuntimeDetector(fakeClient);

        // Process tree: runner (PID 46588) is located under Ollama directory
        var runnerProc = new GpuProcessSnapshot(
            Pid: 46588,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 1300000000,
            NonLocalGpuMemoryBytes: 200000000,
            TotalCommittedGpuMemoryBytes: 1500000000,
            DedicatedGpuMemoryBytes: 1300000000,
            SharedGpuMemoryBytes: 200000000,
            WorkingSetBytes: 1000000000,
            CpuPercent: 1.0,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\Ollama\lib\ollama\llama-server.exe",
            CommandLine: ""
        );

        var telemetrySnapshot = new SystemSnapshot(DateTimeOffset.UtcNow,
            Gpus: [],
            Memory: new SystemMemorySnapshot(null, null, null),
            GpuProcesses: [runnerProc],
            Warnings: []
        );

        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(telemetrySnapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Single(result.IdentifiedProcesses);
        var proc = result.IdentifiedProcesses[0];
        Assert.Equal(46588, proc.Pid);
        Assert.Equal(AiRuntimeKind.Ollama, proc.Runtime);
        Assert.Equal(DetectionConfidence.Confirmed, proc.RuntimeConfidence);
        Assert.NotNull(proc.Model);
        Assert.Equal("qwen2.5:1.5b", proc.Model.DisplayName);
        Assert.Equal("Q4_K_M", proc.Model.Quantization);
        Assert.Equal("1.5B", proc.Model.ParameterSize);
        Assert.Equal(32768, proc.Model.ContextLength);
        Assert.Equal(1300000000UL, proc.Model.RuntimeReportedVramBytes);
        Assert.Empty(result.UnmappedModels);
    }

    [Fact]
    public async Task OllamaRuntimeDetector_MultiModelMultiRunner_DoesNotGuess()
    {
        var psResponse = new OllamaPsResponse
        {
            Models =
            [
                new() { Name = "qwen2.5:14b", ContextLength = 32768 },
                new() { Name = "llama3.2:3b", ContextLength = 8192 }
            ]
        };

        var fakeClient = new FakeOllamaApiClient(psResponse);
        var detector = new OllamaRuntimeDetector(fakeClient);

        var runner1 = new GpuProcessSnapshot(
            Pid: 100,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 5000000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 5000000000,
            DedicatedGpuMemoryBytes: 5000000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\Ollama\lib\ollama\llama-server.exe",
            CommandLine: ""
        );
        var runner2 = new GpuProcessSnapshot(
            Pid: 200,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 2000000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 2000000000,
            DedicatedGpuMemoryBytes: 2000000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\Ollama\lib\ollama\llama-server.exe",
            CommandLine: ""
        );

        var telemetrySnapshot = new SystemSnapshot(DateTimeOffset.UtcNow,
            Gpus: [],
            Memory: new SystemMemorySnapshot(null, null, null),
            GpuProcesses: [runner1, runner2],
            Warnings: []
        );

        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(telemetrySnapshot, relationships);

        var result = await detector.DetectAsync(context, CancellationToken.None);

        Assert.Equal(2, result.IdentifiedProcesses.Count);
        // Both runners must be Ollama, but with Model = null (UNKNOWN > WRONG)
        foreach (var p in result.IdentifiedProcesses)
        {
            Assert.Equal(AiRuntimeKind.Ollama, p.Runtime);
            Assert.Null(p.Model);
        }

        // Both models preserved in UnmappedModels
        Assert.Equal(2, result.UnmappedModels.Count);
    }

    private sealed class TrackingOllamaApiClient : IOllamaApiClient
    {
        public int CallCount { get; private set; }
        public OllamaPsResponse? NextResponse { get; set; }

        public TrackingOllamaApiClient(OllamaPsResponse? initialResponse)
        {
            NextResponse = initialResponse;
        }

        public Task<OllamaPsResponse?> GetLoadedModelsAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(NextResponse);
        }
    }

    [Fact]
    public async Task OllamaRuntimeDetector_RunnerPidChange_InvalidatesCacheAndQueriesApi()
    {
        var modelA = new OllamaPsResponse
        {
            Models = [new() { Name = "qwen2.5:1.5b", SizeVram = 1300000000UL, ContextLength = 32768 }]
        };
        var modelB = new OllamaPsResponse
        {
            Models = [new() { Name = "llama3.2:3b", SizeVram = 2500000000UL, ContextLength = 8192 }]
        };

        var trackingClient = new TrackingOllamaApiClient(modelA);
        var detector = new OllamaRuntimeDetector(trackingClient, TimeSpan.FromSeconds(10));

        var runner100 = new GpuProcessSnapshot(
            Pid: 100,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 1300000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 1300000000,
            DedicatedGpuMemoryBytes: 1300000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\Ollama\lib\ollama\llama-server.exe",
            CommandLine: ""
        );

        var snapshot1 = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [runner100], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context1 = new WorkloadDetectionContext(snapshot1, relationships);

        var result1 = await detector.DetectAsync(context1, CancellationToken.None);
        Assert.Single(result1.IdentifiedProcesses);
        Assert.Equal("qwen2.5:1.5b", result1.IdentifiedProcesses[0].Model?.DisplayName);
        Assert.Equal(1, trackingClient.CallCount);

        // Step 2: Runner PID 100 disappears, runner PID 200 appears
        trackingClient.NextResponse = modelB;
        var runner200 = new GpuProcessSnapshot(
            Pid: 200,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 2500000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 2500000000,
            DedicatedGpuMemoryBytes: 2500000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\Ollama\lib\ollama\llama-server.exe",
            CommandLine: ""
        );

        var snapshot2 = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [runner200], []);
        var context2 = new WorkloadDetectionContext(snapshot2, relationships);

        var result2 = await detector.DetectAsync(context2, CancellationToken.None);
        Assert.Single(result2.IdentifiedProcesses);
        Assert.Equal(200, result2.IdentifiedProcesses[0].Pid);
        Assert.Equal("llama3.2:3b", result2.IdentifiedProcesses[0].Model?.DisplayName);
        Assert.Equal(2, trackingClient.CallCount);
    }

    [Fact]
    public async Task OllamaRuntimeDetector_RunnerPidChange_ApiFails_DoesNotUseStaleModel()
    {
        var modelA = new OllamaPsResponse
        {
            Models = [new() { Name = "qwen2.5:1.5b", SizeVram = 1300000000UL, ContextLength = 32768 }]
        };

        var trackingClient = new TrackingOllamaApiClient(modelA);
        var detector = new OllamaRuntimeDetector(trackingClient, TimeSpan.FromSeconds(10));

        var runner100 = new GpuProcessSnapshot(
            Pid: 100,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 1300000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 1300000000,
            DedicatedGpuMemoryBytes: 1300000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\Ollama\lib\ollama\llama-server.exe",
            CommandLine: ""
        );

        var snapshot1 = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [runner100], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context1 = new WorkloadDetectionContext(snapshot1, relationships);

        var result1 = await detector.DetectAsync(context1, CancellationToken.None);
        Assert.Single(result1.IdentifiedProcesses);
        Assert.Equal("qwen2.5:1.5b", result1.IdentifiedProcesses[0].Model?.DisplayName);

        // Step 2: PID 200 appears, but API fails (returns null)
        trackingClient.NextResponse = null;
        var runner200 = new GpuProcessSnapshot(
            Pid: 200,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 2500000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 2500000000,
            DedicatedGpuMemoryBytes: 2500000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\Ollama\lib\ollama\llama-server.exe",
            CommandLine: ""
        );

        var snapshot2 = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [runner200], []);
        var context2 = new WorkloadDetectionContext(snapshot2, relationships);

        var result2 = await detector.DetectAsync(context2, CancellationToken.None);
        Assert.Single(result2.IdentifiedProcesses);
        Assert.Equal(200, result2.IdentifiedProcesses[0].Pid);
        Assert.Equal(AiRuntimeKind.Ollama, result2.IdentifiedProcesses[0].Runtime);
        // CRITICAL: MUST NOT use stale model from PID 100!
        Assert.Null(result2.IdentifiedProcesses[0].Model);
        Assert.Equal(DetectionConfidence.None, result2.IdentifiedProcesses[0].ModelConfidence);
    }

    [Fact]
    public async Task OllamaRuntimeDetector_SameRunnerWithinTtl_ReusesCacheWithoutQueryingApi()
    {
        var modelA = new OllamaPsResponse
        {
            Models = [new() { Name = "qwen2.5:1.5b", SizeVram = 1300000000UL, ContextLength = 32768 }]
        };

        var trackingClient = new TrackingOllamaApiClient(modelA);
        var detector = new OllamaRuntimeDetector(trackingClient, TimeSpan.FromSeconds(10));

        var runner100 = new GpuProcessSnapshot(
            Pid: 100,
            ProcessName: "llama-server.exe",
            LocalGpuMemoryBytes: 1300000000,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: 1300000000,
            DedicatedGpuMemoryBytes: 1300000000,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: null,
            CpuPercent: null,
            ExecutablePath: @"C:\Users\User\AppData\Local\Programs\Ollama\lib\ollama\llama-server.exe",
            CommandLine: ""
        );

        var snapshot = new SystemSnapshot(DateTimeOffset.UtcNow, [], new SystemMemorySnapshot(null, null, null), [runner100], []);
        var relationships = new ProcessRelationshipSnapshot(new Dictionary<int, int>());
        var context = new WorkloadDetectionContext(snapshot, relationships);

        var result1 = await detector.DetectAsync(context, CancellationToken.None);
        Assert.Single(result1.IdentifiedProcesses);
        Assert.Equal(1, trackingClient.CallCount);

        var result2 = await detector.DetectAsync(context, CancellationToken.None);
        Assert.Single(result2.IdentifiedProcesses);
        Assert.Equal(1, trackingClient.CallCount);
    }
}
