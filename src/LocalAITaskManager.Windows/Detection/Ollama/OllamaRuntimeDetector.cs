using System.Diagnostics;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Windows.Detection.Ollama;

public sealed class OllamaRuntimeDetector : IRuntimeDetector
{
    private readonly IOllamaApiClient _apiClient;
    private readonly TimeSpan _cacheDuration;
    private DateTimeOffset _lastProbeTime = DateTimeOffset.MinValue;
    private OllamaPsResponse? _cachedResponse;
    private HashSet<int> _lastRunnerPids = [];

    public AiRuntimeKind Runtime => AiRuntimeKind.Ollama;

    public OllamaRuntimeDetector(IOllamaApiClient apiClient, TimeSpan? cacheDuration = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _cacheDuration = cacheDuration ?? TimeSpan.FromSeconds(1.5);
    }

    public async Task<RuntimeDetectionResult> DetectAsync(
        WorkloadDetectionContext context,
        CancellationToken cancellationToken)
    {
        var identified = new List<AiProcessIdentity>();
        var runnerCandidates = new List<(GpuProcessSnapshot Process, List<DetectionEvidence> Evidence, int? RuntimeRootPid)>();

        HashSet<int> ollamaPids = FindOllamaProcessIds();

        foreach (GpuProcessSnapshot process in context.TelemetrySnapshot.GpuProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool isOllama = false;
            int? runtimeRootPid = null;
            var evidence = new List<DetectionEvidence>();

            string procName = process.ProcessName;
            if (procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                procName = procName[..^4];
            }

            bool isDirectController = procName.Equals("ollama", StringComparison.OrdinalIgnoreCase) ||
                                      procName.Equals("ollama app", StringComparison.OrdinalIgnoreCase);

            if (isDirectController)
            {
                isOllama = true;
                runtimeRootPid = process.Pid;
                evidence.Add(new(DetectionEvidenceKind.ExecutableName, $"Process is {process.ProcessName}"));
            }

            if (!string.IsNullOrWhiteSpace(process.ExecutablePath) &&
                process.ExecutablePath.Contains(@"\Ollama\", StringComparison.OrdinalIgnoreCase))
            {
                isOllama = true;
                evidence.Add(new(DetectionEvidenceKind.ExecutablePath, "Executable located within Ollama installation directory"));
            }

            foreach (int ollamaPid in ollamaPids)
            {
                if (context.ProcessRelationships.IsDescendantOf(process.Pid, ollamaPid))
                {
                    isOllama = true;
                    runtimeRootPid = ollamaPid;
                    evidence.Add(new(DetectionEvidenceKind.ProcessAncestry, $"Process is a descendant of Ollama (PID {ollamaPid})"));
                    break;
                }
            }

            if (isOllama)
            {
                bool isRunner = !isDirectController;

                if (isRunner)
                {
                    runnerCandidates.Add((process, evidence, runtimeRootPid));
                }
                else
                {
                    identified.Add(new AiProcessIdentity(
                        Pid: process.Pid,
                        Runtime: AiRuntimeKind.Ollama,
                        RuntimeConfidence: DetectionConfidence.Confirmed,
                        Model: null,
                        ModelConfidence: DetectionConfidence.None,
                        Evidence: evidence,
                        RuntimeRootPid: runtimeRootPid ?? process.Pid
                    ));
                }
            }
        }

        HashSet<int> currentRunnerPids = runnerCandidates.Select(r => r.Process.Pid).ToHashSet();
        bool runnerSetChanged = !_lastRunnerPids.SetEquals(currentRunnerPids);

        if (runnerSetChanged)
        {
            _cachedResponse = null;
            _lastRunnerPids = currentRunnerPids;
        }

        bool hasOllamaActivity = ollamaPids.Count > 0 || runnerCandidates.Count > 0 || identified.Count > 0;
        if (!hasOllamaActivity)
        {
            _cachedResponse = null;
            _lastRunnerPids.Clear();
        }

        OllamaPsResponse? psResponse = null;

        if (hasOllamaActivity)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (runnerSetChanged || (now - _lastProbeTime >= _cacheDuration) || _cachedResponse == null)
            {
                psResponse = await _apiClient.GetLoadedModelsAsync(cancellationToken).ConfigureAwait(false);
                _cachedResponse = psResponse;
                _lastProbeTime = now;
            }
            else
            {
                psResponse = _cachedResponse;
            }
        }

        var unmappedModels = new List<RuntimeModelObservation>();
        List<OllamaModelItem> loadedModels = psResponse?.Models ?? [];

        if (loadedModels.Count == 1 && runnerCandidates.Count == 1)
        {
            var (runnerProcess, runnerEvidence, runnerRootPid) = runnerCandidates[0];
            OllamaModelItem modelItem = loadedModels[0];

            runnerEvidence.Add(new(DetectionEvidenceKind.LocalApi, "Ollama /api/ps confirmed one loaded model"));
            runnerEvidence.Add(new(DetectionEvidenceKind.LocalApi, "Unambiguous 1:1 runner to model correlation"));

            var modelIdentity = new DetectedModelIdentity(
                DisplayName: modelItem.Name ?? modelItem.Model ?? "Unknown Ollama Model",
                ModelPath: null,
                Quantization: modelItem.Details?.QuantizationLevel,
                Architecture: modelItem.Details?.Family,
                ParameterSize: modelItem.Details?.ParameterSize,
                ContextLength: modelItem.ContextLength,
                RuntimeReportedVramBytes: modelItem.SizeVram
            );

            identified.Add(new AiProcessIdentity(
                Pid: runnerProcess.Pid,
                Runtime: AiRuntimeKind.Ollama,
                RuntimeConfidence: DetectionConfidence.Confirmed,
                Model: modelIdentity,
                ModelConfidence: DetectionConfidence.Confirmed,
                Evidence: runnerEvidence,
                RuntimeRootPid: runnerRootPid
            ));
        }
        else
        {
            if (loadedModels.Count > 1 || (loadedModels.Count > 0 && runnerCandidates.Count != loadedModels.Count))
            {
                foreach (OllamaModelItem item in loadedModels)
                {
                    var modelIdentity = new DetectedModelIdentity(
                        DisplayName: item.Name ?? item.Model ?? "Unknown Ollama Model",
                        ModelPath: null,
                        Quantization: item.Details?.QuantizationLevel,
                        Architecture: item.Details?.Family,
                        ParameterSize: item.Details?.ParameterSize,
                        ContextLength: item.ContextLength,
                        RuntimeReportedVramBytes: item.SizeVram
                    );
                    unmappedModels.Add(new RuntimeModelObservation(AiRuntimeKind.Ollama, modelIdentity, item.SizeVram));
                }
            }

            foreach (var (runnerProcess, runnerEvidence, runnerRootPid) in runnerCandidates)
            {
                if (loadedModels.Count > 1)
                {
                    runnerEvidence.Add(new(DetectionEvidenceKind.LocalApi, "Multiple models loaded in Ollama; runner mapping is ambiguous"));
                }

                identified.Add(new AiProcessIdentity(
                    Pid: runnerProcess.Pid,
                    Runtime: AiRuntimeKind.Ollama,
                    RuntimeConfidence: DetectionConfidence.Confirmed,
                    Model: null,
                    ModelConfidence: DetectionConfidence.None,
                    Evidence: runnerEvidence,
                    RuntimeRootPid: runnerRootPid
                ));
            }
        }

        return new RuntimeDetectionResult(identified, unmappedModels, []);
    }

    private static HashSet<int> FindOllamaProcessIds()
    {
        var pids = new HashSet<int>();
        try
        {
            foreach (Process p in Process.GetProcessesByName("ollama"))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
            foreach (Process p in Process.GetProcessesByName("ollama app"))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
        }
        catch
        {
            // Best effort
        }
        return pids;
    }
}
