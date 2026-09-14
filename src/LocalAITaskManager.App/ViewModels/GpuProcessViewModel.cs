using System.Collections.ObjectModel;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.App.ViewModels;

public sealed class GpuProcessViewModel : ViewModelBase
{
    private int _pid;
    private string _processName = string.Empty;
    private ulong? _localVramBytes;
    private string _vramText = "—";
    private string _runtimeText = "—";
    private string _modelText = "—";
    private string _localUsageText = "—";
    private string _nonLocalUsageText = "—";
    private string _totalCommittedText = "—";
    private string _dedicatedUsageText = "—";
    private string _sharedUsageText = "—";
    private string _ramText = "—";
    private string _cpuText = "—";
    private string _executablePath = "Unavailable";
    private string _commandLine = "Unavailable";

    // AI Workload Inspector Details
    private bool _isAiWorkload;
    private string _aiRuntimeText = "—";
    private string _aiRuntimeConfidenceText = "—";
    private string _aiModelNameText = "—";
    private string _aiModelConfidenceText = "—";
    private string _aiQuantizationText = "—";
    private string _aiParameterSizeText = "—";
    private string _aiConfiguredContextText = "—";
    private string _aiRuntimeReportedVramText = "—";

    public ObservableCollection<string> AiEvidence { get; } = [];

    public int Pid
    {
        get => _pid;
        set => SetProperty(ref _pid, value);
    }

    public string ProcessName
    {
        get => _processName;
        set => SetProperty(ref _processName, value);
    }

    public string RuntimeText
    {
        get => _runtimeText;
        set => SetProperty(ref _runtimeText, value);
    }

    public string ModelText
    {
        get => _modelText;
        set => SetProperty(ref _modelText, value);
    }

    public ulong? LocalVramBytes
    {
        get => _localVramBytes;
        set => SetProperty(ref _localVramBytes, value);
    }

    public string VramText
    {
        get => _vramText;
        set => SetProperty(ref _vramText, value);
    }

    public string LocalUsageText
    {
        get => _localUsageText;
        set => SetProperty(ref _localUsageText, value);
    }

    public string NonLocalUsageText
    {
        get => _nonLocalUsageText;
        set => SetProperty(ref _nonLocalUsageText, value);
    }

    public string TotalCommittedText
    {
        get => _totalCommittedText;
        set => SetProperty(ref _totalCommittedText, value);
    }

    public string DedicatedUsageText
    {
        get => _dedicatedUsageText;
        set => SetProperty(ref _dedicatedUsageText, value);
    }

    public string SharedUsageText
    {
        get => _sharedUsageText;
        set => SetProperty(ref _sharedUsageText, value);
    }

    public string RamText
    {
        get => _ramText;
        set => SetProperty(ref _ramText, value);
    }

    public string CpuText
    {
        get => _cpuText;
        set => SetProperty(ref _cpuText, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public string CommandLine
    {
        get => _commandLine;
        set => SetProperty(ref _commandLine, value);
    }

    public bool IsAiWorkload
    {
        get => _isAiWorkload;
        set => SetProperty(ref _isAiWorkload, value);
    }

    public string AiRuntimeText
    {
        get => _aiRuntimeText;
        set => SetProperty(ref _aiRuntimeText, value);
    }

    public string AiRuntimeConfidenceText
    {
        get => _aiRuntimeConfidenceText;
        set => SetProperty(ref _aiRuntimeConfidenceText, value);
    }

    public string AiModelNameText
    {
        get => _aiModelNameText;
        set => SetProperty(ref _aiModelNameText, value);
    }

    public string AiModelConfidenceText
    {
        get => _aiModelConfidenceText;
        set => SetProperty(ref _aiModelConfidenceText, value);
    }

    public string AiQuantizationText
    {
        get => _aiQuantizationText;
        set => SetProperty(ref _aiQuantizationText, value);
    }

    public string AiParameterSizeText
    {
        get => _aiParameterSizeText;
        set => SetProperty(ref _aiParameterSizeText, value);
    }

    public string AiConfiguredContextText
    {
        get => _aiConfiguredContextText;
        set => SetProperty(ref _aiConfiguredContextText, value);
    }

    public string AiRuntimeReportedVramText
    {
        get => _aiRuntimeReportedVramText;
        set => SetProperty(ref _aiRuntimeReportedVramText, value);
    }

    public void UpdateFromSnapshot(GpuProcessSnapshot snapshot, AiProcessIdentity? aiIdentity = null)
    {
        Pid = snapshot.Pid;
        ProcessName = snapshot.ProcessName;
        LocalVramBytes = snapshot.LocalGpuMemoryBytes;

        // Primary table column uses Local Usage
        VramText = ByteFormatter.Format(snapshot.LocalGpuMemoryBytes, "—");

        LocalUsageText = ByteFormatter.Format(snapshot.LocalGpuMemoryBytes, "—");
        NonLocalUsageText = ByteFormatter.Format(snapshot.NonLocalGpuMemoryBytes, "—");
        TotalCommittedText = ByteFormatter.Format(snapshot.TotalCommittedGpuMemoryBytes, "—");
        DedicatedUsageText = ByteFormatter.Format(snapshot.DedicatedGpuMemoryBytes, "—");
        SharedUsageText = ByteFormatter.Format(snapshot.SharedGpuMemoryBytes, "—");

        RamText = ByteFormatter.Format(snapshot.WorkingSetBytes, "—");
        CpuText = snapshot.CpuPercent.HasValue ? $"{snapshot.CpuPercent.Value:0}%" : "—";
        ExecutablePath = !string.IsNullOrWhiteSpace(snapshot.ExecutablePath) ? snapshot.ExecutablePath : "Unavailable";
        CommandLine = !string.IsNullOrWhiteSpace(snapshot.CommandLine) ? snapshot.CommandLine : "Unavailable";

        // AI Workload attribution
        if (aiIdentity is not null && aiIdentity.Runtime != AiRuntimeKind.Unknown)
        {
            IsAiWorkload = true;
            RuntimeText = aiIdentity.Runtime switch
            {
                AiRuntimeKind.LlamaCpp => "llama.cpp",
                AiRuntimeKind.Ollama => "Ollama",
                AiRuntimeKind.LmStudio => "LM Studio",
                _ => "—"
            };
            ModelText = aiIdentity.Model?.DisplayName ?? "—";

            AiRuntimeText = RuntimeText;
            AiRuntimeConfidenceText = aiIdentity.RuntimeConfidence.ToString();
            AiModelNameText = aiIdentity.Model?.DisplayName ?? "—";
            AiModelConfidenceText = aiIdentity.ModelConfidence != DetectionConfidence.None ? aiIdentity.ModelConfidence.ToString() : "—";
            AiQuantizationText = aiIdentity.Model?.Quantization ?? "—";
            AiParameterSizeText = aiIdentity.Model?.ParameterSize ?? "—";
            AiConfiguredContextText = aiIdentity.Model?.ContextLength.HasValue == true
                ? FormatContextLength(aiIdentity.Model.ContextLength.Value)
                : "—";
            AiRuntimeReportedVramText = ByteFormatter.Format(aiIdentity.Model?.RuntimeReportedVramBytes, "—");

            AiEvidence.Clear();
            foreach (var ev in aiIdentity.Evidence)
            {
                AiEvidence.Add(ev.Description);
            }
        }
        else
        {
            IsAiWorkload = false;
            RuntimeText = "—";
            ModelText = "—";
            AiRuntimeText = "—";
            AiRuntimeConfidenceText = "—";
            AiModelNameText = "—";
            AiModelConfidenceText = "—";
            AiQuantizationText = "—";
            AiParameterSizeText = "—";
            AiConfiguredContextText = "—";
            AiRuntimeReportedVramText = "—";
            AiEvidence.Clear();
        }
    }

    private static string FormatContextLength(int contextLength)
    {
        if (contextLength >= 1024 && contextLength % 1024 == 0)
        {
            return $"{contextLength / 1024}K";
        }
        return contextLength.ToString("N0");
    }
}
