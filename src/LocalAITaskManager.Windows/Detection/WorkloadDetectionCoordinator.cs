using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Windows.Detection;

public sealed class WorkloadDetectionCoordinator : IWorkloadDetectionCoordinator
{
    private readonly IProcessRelationshipProvider _relationshipProvider;
    private readonly IReadOnlyList<IRuntimeDetector> _detectors;

    public WorkloadDetectionCoordinator(
        IProcessRelationshipProvider relationshipProvider,
        IEnumerable<IRuntimeDetector> detectors)
    {
        _relationshipProvider = relationshipProvider ?? throw new ArgumentNullException(nameof(relationshipProvider));
        _detectors = detectors?.ToList() ?? throw new ArgumentNullException(nameof(detectors));
    }

    public async Task<DetectionSnapshot> DetectWorkloadsAsync(
        SystemSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IProcessRelationshipSnapshot relationships = _relationshipProvider.GetSnapshot();
        var context = new WorkloadDetectionContext(snapshot, relationships);

        var candidateMap = new Dictionary<int, List<AiProcessIdentity>>();
        var unmappedModels = new List<RuntimeModelObservation>();
        var warnings = new List<DetectionWarning>();

        foreach (IRuntimeDetector detector in _detectors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                RuntimeDetectionResult result = await detector.DetectAsync(context, cancellationToken).ConfigureAwait(false);

                foreach (AiProcessIdentity identified in result.IdentifiedProcesses)
                {
                    if (!candidateMap.TryGetValue(identified.Pid, out var list))
                    {
                        list = [];
                        candidateMap[identified.Pid] = list;
                    }
                    list.Add(identified);
                }

                unmappedModels.AddRange(result.UnmappedModels);
                warnings.AddRange(result.Warnings);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                warnings.Add(new DetectionWarning(detector.Runtime.ToString(), $"Detector failed: {ex.Message}"));
            }
        }

        var resolvedMap = new Dictionary<int, AiProcessIdentity>();

        foreach (var (pid, candidates) in candidateMap)
        {
            if (candidates.Count == 1)
            {
                resolvedMap[pid] = candidates[0];
            }
            else
            {
                // Resolve conflict:
                // 1. Confirmed > High > Medium > Low
                // 2. Specific runtime (Ollama, LM Studio) > generic (LlamaCpp)
                AiProcessIdentity winner = candidates
                    .OrderByDescending(c => (int)c.RuntimeConfidence)
                    .ThenByDescending(c => GetRuntimeSpecificity(c.Runtime))
                    .First();

                resolvedMap[pid] = winner;
            }
        }

        return new DetectionSnapshot(
            Timestamp: DateTimeOffset.UtcNow,
            Processes: resolvedMap,
            UnmappedModels: unmappedModels,
            Warnings: warnings
        );
    }

    private static int GetRuntimeSpecificity(AiRuntimeKind runtime)
    {
        return runtime switch
        {
            AiRuntimeKind.Ollama => 2,
            AiRuntimeKind.LmStudio => 2,
            AiRuntimeKind.LlamaCpp => 1,
            _ => 0
        };
    }
}
