using System.Diagnostics;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;

namespace LocalAITaskManager.Core.Services;

public sealed class AiWorkloadComposer : IAiWorkloadComposer
{
    public IReadOnlyList<AiWorkloadSnapshot> Compose(
        SystemSnapshot telemetry,
        DetectionSnapshot detection)
    {
        var processLookup = telemetry.GpuProcesses
            .GroupBy(p => p.Pid)
            .ToDictionary(g => g.Key, g => g.First());

        var instanceGroups = detection.Processes.Values
            .GroupBy(p => (Runtime: p.Runtime, Root: p.RuntimeRootPid ?? p.Pid))
            .ToList();

        var workloads = new List<AiWorkloadSnapshot>();

        foreach (var group in instanceGroups)
        {
            var runtime = group.Key.Runtime;
            int groupRoot = group.Key.Root;
            var groupProcesses = group.ToList();

            var modelProcesses = groupProcesses
                .Where(p => p.Model != null)
                .OrderBy(p => p.Pid)
                .ToList();

            var nonModelProcesses = groupProcesses
                .Where(p => p.Model == null)
                .OrderBy(p => p.Pid)
                .ToList();

            if (modelProcesses.Count == 1)
            {
                var modelProc = modelProcesses[0];
                var pids = new List<int> { modelProc.Pid };
                foreach (var n in nonModelProcesses)
                {
                    pids.Add(n.Pid);
                }

                processLookup.TryGetValue(modelProc.Pid, out var primarySnap);

                workloads.Add(new AiWorkloadSnapshot(
                    WorkloadId: $"{runtime.ToString().ToLowerInvariant()}:model:{modelProc.Pid}",
                    Kind: AiWorkloadKind.Model,
                    Runtime: runtime,
                    DisplayName: modelProc.Model?.DisplayName ?? $"{runtime} model",
                    PrimaryPid: modelProc.Pid,
                    RuntimeRootPid: modelProc.RuntimeRootPid,
                    Model: modelProc.Model,
                    RuntimeConfidence: modelProc.RuntimeConfidence,
                    ModelConfidence: modelProc.ModelConfidence,
                    ProcessPids: pids,
                    PrimaryLocalGpuMemoryBytes: primarySnap?.LocalGpuMemoryBytes,
                    PrimaryWorkingSetBytes: primarySnap?.WorkingSetBytes,
                    PrimaryCpuPercent: primarySnap?.CpuPercent,
                    Evidence: CombineEvidence(groupProcesses.SelectMany(p => p.Evidence))
                ));
            }
            else if (modelProcesses.Count > 1)
            {
                foreach (var modelProc in modelProcesses)
                {
                    processLookup.TryGetValue(modelProc.Pid, out var primarySnap);

                    workloads.Add(new AiWorkloadSnapshot(
                        WorkloadId: $"{runtime.ToString().ToLowerInvariant()}:model:{modelProc.Pid}",
                        Kind: AiWorkloadKind.Model,
                        Runtime: runtime,
                        DisplayName: modelProc.Model?.DisplayName ?? $"{runtime} model",
                        PrimaryPid: modelProc.Pid,
                        RuntimeRootPid: modelProc.RuntimeRootPid,
                        Model: modelProc.Model,
                        RuntimeConfidence: modelProc.RuntimeConfidence,
                        ModelConfidence: modelProc.ModelConfidence,
                        ProcessPids: [modelProc.Pid],
                        PrimaryLocalGpuMemoryBytes: primarySnap?.LocalGpuMemoryBytes,
                        PrimaryWorkingSetBytes: primarySnap?.WorkingSetBytes,
                        PrimaryCpuPercent: primarySnap?.CpuPercent,
                        Evidence: CombineEvidence(modelProc.Evidence)
                    ));
                }

                if (nonModelProcesses.Count > 0)
                {
                    int primaryServicePid = SelectPrimaryPid(nonModelProcesses, groupRoot, processLookup);
                    processLookup.TryGetValue(primaryServicePid, out var primarySnap);

                    DetectionConfidence maxRuntimeConfidence = nonModelProcesses.Max(p => p.RuntimeConfidence);

                    workloads.Add(new AiWorkloadSnapshot(
                        WorkloadId: $"{runtime.ToString().ToLowerInvariant()}:service:{groupRoot}",
                        Kind: AiWorkloadKind.RuntimeService,
                        Runtime: runtime,
                        DisplayName: GetRuntimeServicesDisplayName(runtime),
                        PrimaryPid: primaryServicePid,
                        RuntimeRootPid: groupRoot,
                        Model: null,
                        RuntimeConfidence: maxRuntimeConfidence,
                        ModelConfidence: DetectionConfidence.None,
                        ProcessPids: nonModelProcesses.Select(p => p.Pid).ToList(),
                        PrimaryLocalGpuMemoryBytes: primarySnap?.LocalGpuMemoryBytes,
                        PrimaryWorkingSetBytes: primarySnap?.WorkingSetBytes,
                        PrimaryCpuPercent: primarySnap?.CpuPercent,
                        Evidence: CombineEvidence(nonModelProcesses.SelectMany(p => p.Evidence))
                    ));
                }
            }
            else
            {
                int primaryPid = SelectPrimaryPid(nonModelProcesses, groupRoot, processLookup);
                processLookup.TryGetValue(primaryPid, out var primarySnap);

                DetectionConfidence maxRuntimeConfidence = nonModelProcesses.Max(p => p.RuntimeConfidence);
                int? runtimeRoot = nonModelProcesses.FirstOrDefault(p => p.RuntimeRootPid != null)?.RuntimeRootPid;

                workloads.Add(new AiWorkloadSnapshot(
                    WorkloadId: $"{runtime.ToString().ToLowerInvariant()}:runtime:{groupRoot}",
                    Kind: AiWorkloadKind.RuntimeOnly,
                    Runtime: runtime,
                    DisplayName: GetRuntimeDisplayName(runtime),
                    PrimaryPid: primaryPid,
                    RuntimeRootPid: runtimeRoot,
                    Model: null,
                    RuntimeConfidence: maxRuntimeConfidence,
                    ModelConfidence: DetectionConfidence.None,
                    ProcessPids: nonModelProcesses.Select(p => p.Pid).ToList(),
                    PrimaryLocalGpuMemoryBytes: primarySnap?.LocalGpuMemoryBytes,
                    PrimaryWorkingSetBytes: primarySnap?.WorkingSetBytes,
                    PrimaryCpuPercent: primarySnap?.CpuPercent,
                    Evidence: CombineEvidence(nonModelProcesses.SelectMany(p => p.Evidence))
                ));
            }
        }

        workloads.Sort((a, b) =>
        {
            int kindCompare = GetKindOrder(a.Kind).CompareTo(GetKindOrder(b.Kind));
            if (kindCompare != 0) return kindCompare;

            if (a.PrimaryLocalGpuMemoryBytes.HasValue && b.PrimaryLocalGpuMemoryBytes.HasValue)
            {
                int vramCompare = b.PrimaryLocalGpuMemoryBytes.Value.CompareTo(a.PrimaryLocalGpuMemoryBytes.Value);
                if (vramCompare != 0) return vramCompare;
            }
            else if (a.PrimaryLocalGpuMemoryBytes.HasValue)
            {
                return -1;
            }
            else if (b.PrimaryLocalGpuMemoryBytes.HasValue)
            {
                return 1;
            }

            return string.Compare(a.WorkloadId, b.WorkloadId, StringComparison.Ordinal);
        });

        return workloads;
    }

    private static int SelectPrimaryPid(
        IReadOnlyList<AiProcessIdentity> processes,
        int? rootPid,
        IReadOnlyDictionary<int, GpuProcessSnapshot> processLookup)
    {
        if (processes.Count == 0)
        {
            return 0;
        }

        if (processes.Count == 1)
        {
            return processes[0].Pid;
        }

        var processesWithGpu = processes
            .Where(p => processLookup.TryGetValue(p.Pid, out var snap) && snap.LocalGpuMemoryBytes.HasValue)
            .Select(p => (p.Pid, Memory: processLookup[p.Pid].LocalGpuMemoryBytes!.Value))
            .ToList();

        if (processesWithGpu.Count > 0)
        {
            ulong maxMemory = processesWithGpu.Max(x => x.Memory);
            var tiedPids = processesWithGpu
                .Where(x => x.Memory == maxMemory)
                .Select(x => x.Pid)
                .ToList();

            if (rootPid.HasValue && tiedPids.Contains(rootPid.Value))
            {
                return rootPid.Value;
            }

            return tiedPids.Min();
        }

        if (rootPid.HasValue && processes.Any(p => p.Pid == rootPid.Value))
        {
            return rootPid.Value;
        }

        return processes.Min(p => p.Pid);
    }

    private static IReadOnlyList<DetectionEvidence> CombineEvidence(IEnumerable<DetectionEvidence> evidenceList)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DetectionEvidence>();
        foreach (var ev in evidenceList)
        {
            string key = $"{ev.Kind}:{ev.Description}";
            if (seen.Add(key))
            {
                result.Add(ev);
            }
        }
        return result;
    }

    private static int GetKindOrder(AiWorkloadKind kind) => kind switch
    {
        AiWorkloadKind.Model => 0,
        AiWorkloadKind.RuntimeOnly => 1,
        AiWorkloadKind.RuntimeService => 2,
        _ => 3
    };

    private static string GetRuntimeDisplayName(AiRuntimeKind runtime) => runtime switch
    {
        AiRuntimeKind.Ollama => "Ollama",
        AiRuntimeKind.LmStudio => "LM Studio",
        AiRuntimeKind.LlamaCpp => "llama.cpp",
        _ => runtime.ToString()
    };

    private static string GetRuntimeServicesDisplayName(AiRuntimeKind runtime) => runtime switch
    {
        AiRuntimeKind.Ollama => "Ollama runtime services",
        AiRuntimeKind.LmStudio => "LM Studio runtime services",
        AiRuntimeKind.LlamaCpp => "llama.cpp runtime services",
        _ => $"{runtime} runtime services"
    };
}
