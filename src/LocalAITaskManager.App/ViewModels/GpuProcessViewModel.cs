using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.App.ViewModels;

public sealed class GpuProcessViewModel : ViewModelBase
{
    private int _pid;
    private string _processName = string.Empty;
    private ulong? _localVramBytes;
    private string _vramText = "—";
    private string _localUsageText = "—";
    private string _nonLocalUsageText = "—";
    private string _totalCommittedText = "—";
    private string _dedicatedUsageText = "—";
    private string _sharedUsageText = "—";
    private string _ramText = "—";
    private string _cpuText = "—";
    private string _executablePath = "Unavailable";
    private string _commandLine = "Unavailable";

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

    public ulong? LocalVramBytes
    {
        get => _localVramBytes;
        set => SetProperty(ref _localVramBytes, value);
    }

    /// <summary>
    /// Primary process VRAM column text based on WDDM Local Usage.
    /// </summary>
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

    public void UpdateFromSnapshot(GpuProcessSnapshot snapshot)
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
    }
}
