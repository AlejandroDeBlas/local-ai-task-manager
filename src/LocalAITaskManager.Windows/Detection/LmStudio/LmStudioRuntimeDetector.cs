using System.Diagnostics;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.Windows.Detection.LmStudio;

public sealed class LmStudioRuntimeDetector : IRuntimeDetector
{
    private readonly ILocalCommandRunner? _commandRunner;
    private readonly TimeSpan _cacheDuration;
    private readonly Func<string?> _cliPathResolver;

    public AiRuntimeKind Runtime => AiRuntimeKind.LmStudio;

    public LmStudioRuntimeDetector(
        ILocalCommandRunner? commandRunner = null,
        TimeSpan? cacheDuration = null,
        Func<string?>? cliPathResolver = null)
    {
        _commandRunner = commandRunner;
        _cacheDuration = cacheDuration ?? TimeSpan.FromSeconds(3);
        _cliPathResolver = cliPathResolver ?? ResolveLmsCliPath;
    }

    public Task<RuntimeDetectionResult> DetectAsync(
        WorkloadDetectionContext context,
        CancellationToken cancellationToken)
    {
        var identified = new List<AiProcessIdentity>();
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

            bool isDirectExecutable = procName.Equals("LM Studio", StringComparison.OrdinalIgnoreCase) ||
                                      procName.Equals("lms", StringComparison.OrdinalIgnoreCase);

            if (isDirectExecutable)
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
                evidence.Add(new(DetectionEvidenceKind.ExecutablePath, "LM Studio model detection is disabled/not validated; model left unassigned"));

                identified.Add(new AiProcessIdentity(
                    Pid: process.Pid,
                    Runtime: AiRuntimeKind.LmStudio,
                    RuntimeConfidence: isDirectExecutable ? DetectionConfidence.Confirmed : DetectionConfidence.Medium,
                    Model: null,
                    ModelConfidence: DetectionConfidence.None,
                    Evidence: evidence
                ));
            }
        }

        return Task.FromResult(new RuntimeDetectionResult(identified, [], []));
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
