using System.Collections.ObjectModel;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.App.ViewModels;

public sealed class AiWorkloadViewModel : ViewModelBase
{
    private AiWorkloadSnapshot _snapshot;
    private bool _isSelected;

    public AiWorkloadSnapshot Snapshot => _snapshot;

    public string WorkloadId => _snapshot.WorkloadId;
    public AiWorkloadKind Kind => _snapshot.Kind;
    public AiRuntimeKind Runtime => _snapshot.Runtime;
    public string DisplayName => _snapshot.DisplayName;
    public int PrimaryPid => _snapshot.PrimaryPid;
    public int? RuntimeRootPid => _snapshot.RuntimeRootPid;

    public bool IsModelKind => _snapshot.Kind == AiWorkloadKind.Model;
    public bool IsRuntimeOnlyKind => _snapshot.Kind == AiWorkloadKind.RuntimeOnly;
    public bool IsRuntimeServiceKind => _snapshot.Kind == AiWorkloadKind.RuntimeService;

    public string RuntimeBadgeText => _snapshot.Runtime switch
    {
        AiRuntimeKind.LmStudio => "LM Studio • Experimental",
        AiRuntimeKind.Ollama => "Ollama",
        AiRuntimeKind.LlamaCpp => "llama.cpp",
        _ => _snapshot.Runtime.ToString()
    };

    public string? SubtitleText => _snapshot.Kind switch
    {
        AiWorkloadKind.RuntimeOnly => _snapshot.Runtime == AiRuntimeKind.LmStudio
            ? "Model detection unavailable"
            : "No model detected",
        AiWorkloadKind.RuntimeService => "Shared runtime process",
        _ => null
    };

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(SubtitleText);

    public string? QuantizationBadgeText => _snapshot.Model?.Quantization;
    public bool HasQuantization => !string.IsNullOrWhiteSpace(QuantizationBadgeText);

    public string? ContextBadgeText => _snapshot.Model?.ContextLength != null
        ? $"Context {ByteFormatter.FormatTokens(_snapshot.Model.ContextLength)}"
        : null;
    public bool HasContext => !string.IsNullOrWhiteSpace(ContextBadgeText);

    public string? ParameterSizeText => _snapshot.Model?.ParameterSize;
    public bool HasParameterSize => !string.IsNullOrWhiteSpace(ParameterSizeText);

    public string PrimaryVramText => ByteFormatter.Format(_snapshot.PrimaryLocalGpuMemoryBytes, "—");
    public string PrimaryRamText => ByteFormatter.Format(_snapshot.PrimaryWorkingSetBytes, "—");
    public string PrimaryCpuText => _snapshot.PrimaryCpuPercent != null
        ? $"{_snapshot.PrimaryCpuPercent.Value:F1}%"
        : "—";

    public string ProcessCountText => _snapshot.ProcessPids.Count == 1
        ? "1 process"
        : $"{_snapshot.ProcessPids.Count} processes";

    public string RuntimeReportedVramText => ByteFormatter.Format(_snapshot.Model?.RuntimeReportedVramBytes, "—");
    public bool HasRuntimeReportedVram => _snapshot.Model?.RuntimeReportedVramBytes != null;

    public string RuntimeConfidenceText => _snapshot.RuntimeConfidence.ToString();
    public string ModelConfidenceText => _snapshot.Model != null
        ? _snapshot.ModelConfidence.ToString()
        : "None";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ObservableCollection<string> Evidence { get; } = [];
    public ObservableCollection<GpuProcessViewModel> MemberProcesses { get; } = [];

    public AiWorkloadViewModel(
        AiWorkloadSnapshot snapshot,
        IReadOnlyDictionary<int, GpuProcessSnapshot> processLookup)
    {
        _snapshot = snapshot;
        Update(snapshot, processLookup);
    }

    public void Update(
        AiWorkloadSnapshot snapshot,
        IReadOnlyDictionary<int, GpuProcessSnapshot> processLookup)
    {
        _snapshot = snapshot;

        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(PrimaryPid));
        OnPropertyChanged(nameof(RuntimeRootPid));
        OnPropertyChanged(nameof(IsModelKind));
        OnPropertyChanged(nameof(IsRuntimeOnlyKind));
        OnPropertyChanged(nameof(IsRuntimeServiceKind));
        OnPropertyChanged(nameof(RuntimeBadgeText));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(HasSubtitle));
        OnPropertyChanged(nameof(QuantizationBadgeText));
        OnPropertyChanged(nameof(HasQuantization));
        OnPropertyChanged(nameof(ContextBadgeText));
        OnPropertyChanged(nameof(HasContext));
        OnPropertyChanged(nameof(ParameterSizeText));
        OnPropertyChanged(nameof(HasParameterSize));
        OnPropertyChanged(nameof(PrimaryVramText));
        OnPropertyChanged(nameof(PrimaryRamText));
        OnPropertyChanged(nameof(PrimaryCpuText));
        OnPropertyChanged(nameof(ProcessCountText));
        OnPropertyChanged(nameof(RuntimeReportedVramText));
        OnPropertyChanged(nameof(HasRuntimeReportedVram));
        OnPropertyChanged(nameof(RuntimeConfidenceText));
        OnPropertyChanged(nameof(ModelConfidenceText));

        // Evidence update
        var newEvidence = snapshot.Evidence.Select(e => e.Description).ToList();
        if (!Evidence.SequenceEqual(newEvidence))
        {
            Evidence.Clear();
            foreach (var ev in newEvidence)
            {
                Evidence.Add(ev);
            }
        }

        // Reconcile MemberProcesses
        var currentMemberPids = snapshot.ProcessPids.ToHashSet();
        for (int i = MemberProcesses.Count - 1; i >= 0; i--)
        {
            if (!currentMemberPids.Contains(MemberProcesses[i].Pid))
            {
                MemberProcesses.RemoveAt(i);
            }
        }

        var existingMap = MemberProcesses.ToDictionary(m => m.Pid);
        foreach (int pid in snapshot.ProcessPids)
        {
            processLookup.TryGetValue(pid, out var procSnap);
            if (existingMap.TryGetValue(pid, out var existingVm))
            {
                if (procSnap != null)
                {
                    existingVm.UpdateFromSnapshot(procSnap, null);
                }
            }
            else
            {
                var newVm = new GpuProcessViewModel();
                if (procSnap != null)
                {
                    newVm.UpdateFromSnapshot(procSnap, null);
                }
                else
                {
                    newVm.Pid = pid;
                    newVm.ProcessName = $"PID {pid}";
                }
                MemberProcesses.Add(newVm);
            }
        }
    }
}
