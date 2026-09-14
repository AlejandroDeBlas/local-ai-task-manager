using System.Diagnostics;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.Windows.Detection.LmStudio;

public sealed class LmStudioRuntimeDetector : IRuntimeDetector
{
    private readonly ILocalCommandRunner _commandRunner;
    private readonly TimeSpan _cacheDuration;
    private readonly Func<string?> _cliPathResolver;
    private DateTimeOffset _lastProbeTime = DateTimeOffset.MinValue;
    private List<LmStudioLoadedModel> _cachedModels = [];

    public AiRuntimeKind Runtime => AiRuntimeKind.LmStudio;

    public LmStudioRuntimeDetector(
        ILocalCommandRunner commandRunner,
        TimeSpan? cacheDuration = null,
        Func<string?>? cliPathResolver = null)
    {
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _cacheDuration = cacheDuration ?? TimeSpan.FromSeconds(3);
        _cliPathResolver = cliPathResolver ?? ResolveLmsCliPath;
    }

    public async Task<RuntimeDetectionResult> DetectAsync(
        WorkloadDetectionContext context,
        CancellationToken cancellationToken)
    {
        var identified = new List<AiProcessIdentity>();
        var runnerCandidates = new List<(GpuProcessSnapshot Process, List<DetectionEvidence> Evidence)>();

        HashSet<int> lmsPids = FindLmStudioProcessIds();

        foreach (GpuProcessSnapshot process in context.TelemetrySnapshot.GpuProcesses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool isLmStudio = false;
            var evidence = new List<DetectionEvidence>();

            string procName = process.ProcessName;
            if (procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                procName = procName[..^4];
            }

            if (procName.Equals("LM Studio", StringComparison.OrdinalIgnoreCase) ||
                procName.Equals("lms", StringComparison.OrdinalIgnoreCase))
            {
                isLmStudio = true;
                evidence.Add(new(DetectionEvidenceKind.ExecutableName, $"Process is {process.ProcessName}"));
            }

            if (!string.IsNullOrWhiteSpace(process.ExecutablePath) &&
                process.ExecutablePath.Contains(@"\LM Studio\", StringComparison.OrdinalIgnoreCase))
            {
                isLmStudio = true;
                evidence.Add(new(DetectionEvidenceKind.ExecutablePath, "Executable located within LM Studio directory"));
            }

            foreach (int lmsPid in lmsPids)
            {
                if (context.ProcessRelationships.IsDescendantOf(process.Pid, lmsPid))
                {
                    isLmStudio = true;
                    evidence.Add(new(DetectionEvidenceKind.ProcessAncestry, $"Process is a descendant of LM Studio (PID {lmsPid})"));
                    break;
                }
            }

            if (isLmStudio)
            {
                bool isRunner = !procName.Equals("LM Studio", StringComparison.OrdinalIgnoreCase) &&
                                !procName.Equals("lms", StringComparison.OrdinalIgnoreCase);

                if (isRunner)
                {
                    runnerCandidates.Add((process, evidence));
                }
                else
                {
                    identified.Add(new AiProcessIdentity(
                        Pid: process.Pid,
                        Runtime: AiRuntimeKind.LmStudio,
                        RuntimeConfidence: DetectionConfidence.Confirmed,
                        Model: null,
                        ModelConfidence: DetectionConfidence.None,
                        Evidence: evidence
                    ));
                }
            }
        }

        bool hasLmStudioActivity = lmsPids.Count > 0 || runnerCandidates.Count > 0 || identified.Count > 0;
        List<LmStudioLoadedModel> loadedModels = [];

        if (hasLmStudioActivity)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastProbeTime >= _cacheDuration)
            {
                string? lmsExe = _cliPathResolver();
                if (!string.IsNullOrEmpty(lmsExe))
                {
                    var result = await _commandRunner.ExecuteAsync(
                        lmsExe,
                        ["ps", "--json"],
                        TimeSpan.FromSeconds(2),
                        cancellationToken
                    ).ConfigureAwait(false);

                    if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput))
                    {
                        _cachedModels = LmStudioCliParser.ParseModels(result.StandardOutput);
                    }
                    else
                    {
                        _cachedModels = [];
                    }
                }
                else
                {
                    _cachedModels = [];
                }
                _lastProbeTime = now;
            }

            loadedModels = _cachedModels;
        }

        var unmappedModels = new List<RuntimeModelObservation>();

        if (loadedModels.Count == 1 && runnerCandidates.Count == 1)
        {
            var (runnerProcess, runnerEvidence) = runnerCandidates[0];
            LmStudioLoadedModel modelItem = loadedModels[0];

            runnerEvidence.Add(new(DetectionEvidenceKind.OfficialCli, "LM Studio lms ps --json reported one loaded model"));
            runnerEvidence.Add(new(DetectionEvidenceKind.OfficialCli, "Unambiguous 1:1 runner to model correlation"));

            string displayName = modelItem.Identifier ?? modelItem.ModelKey ??
                (!string.IsNullOrWhiteSpace(modelItem.Path) ? Path.GetFileNameWithoutExtension(modelItem.Path) : "Unknown LM Studio Model");

            string? quant = GgufQuantizationInference.InferFromFileName(modelItem.Path ?? displayName);
            if (quant is not null)
            {
                runnerEvidence.Add(new(DetectionEvidenceKind.FilenameInference, $"Quantization {quant} inferred from model path"));
            }

            var modelIdentity = new DetectedModelIdentity(
                DisplayName: displayName,
                ModelPath: modelItem.Path,
                Quantization: quant,
                Architecture: null,
                ParameterSize: null,
                ContextLength: modelItem.ContextLength,
                RuntimeReportedVramBytes: modelItem.SizeBytes
            );

            identified.Add(new AiProcessIdentity(
                Pid: runnerProcess.Pid,
                Runtime: AiRuntimeKind.LmStudio,
                RuntimeConfidence: DetectionConfidence.Confirmed,
                Model: modelIdentity,
                ModelConfidence: DetectionConfidence.Confirmed,
                Evidence: runnerEvidence
            ));
        }
        else
        {
            if (loadedModels.Count > 1 || (loadedModels.Count > 0 && runnerCandidates.Count != loadedModels.Count))
            {
                foreach (LmStudioLoadedModel item in loadedModels)
                {
                    string displayName = item.Identifier ?? item.ModelKey ??
                        (!string.IsNullOrWhiteSpace(item.Path) ? Path.GetFileNameWithoutExtension(item.Path) : "Unknown LM Studio Model");

                    string? quant = GgufQuantizationInference.InferFromFileName(item.Path ?? displayName);

                    var modelIdentity = new DetectedModelIdentity(
                        DisplayName: displayName,
                        ModelPath: item.Path,
                        Quantization: quant,
                        Architecture: null,
                        ParameterSize: null,
                        ContextLength: item.ContextLength,
                        RuntimeReportedVramBytes: item.SizeBytes
                    );
                    unmappedModels.Add(new RuntimeModelObservation(AiRuntimeKind.LmStudio, modelIdentity, item.SizeBytes));
                }
            }

            foreach (var (runnerProcess, runnerEvidence) in runnerCandidates)
            {
                if (loadedModels.Count > 1)
                {
                    runnerEvidence.Add(new(DetectionEvidenceKind.OfficialCli, "Multiple models loaded in LM Studio; runner mapping is ambiguous"));
                }

                identified.Add(new AiProcessIdentity(
                    Pid: runnerProcess.Pid,
                    Runtime: AiRuntimeKind.LmStudio,
                    RuntimeConfidence: DetectionConfidence.Confirmed,
                    Model: null,
                    ModelConfidence: DetectionConfidence.None,
                    Evidence: runnerEvidence
                ));
            }
        }

        return new RuntimeDetectionResult(identified, unmappedModels, []);
    }

    private static HashSet<int> FindLmStudioProcessIds()
    {
        var pids = new HashSet<int>();
        try
        {
            foreach (Process p in Process.GetProcessesByName("LM Studio"))
            {
                pids.Add(p.Id);
                p.Dispose();
            }
            foreach (Process p in Process.GetProcessesByName("lms"))
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

    private static string? ResolveLmsCliPath()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string cacheLms = Path.Combine(userProfile, ".cache", "lm-studio", "bin", "lms.exe");
        if (File.Exists(cacheLms))
        {
            return cacheLms;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programsLms = Path.Combine(localAppData, "Programs", "LM Studio", "resources", "app", "bin", "lms.exe");
        if (File.Exists(programsLms))
        {
            return programsLms;
        }

        return null;
    }
}
