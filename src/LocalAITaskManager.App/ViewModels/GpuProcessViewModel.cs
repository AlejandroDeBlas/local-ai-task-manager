using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.App.ViewModels;

public sealed class GpuProcessViewModel : ViewModelBase
{
    private int _pid;
    private string _processName = string.Empty;
    private ulong _dedicatedVramBytes;
    private string _vramText = "—";
    private string _sharedVramText = "—";
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

    public ulong DedicatedVramBytes
    {
        get => _dedicatedVramBytes;
        set => SetProperty(ref _dedicatedVramBytes, value);
    }

    public string VramText
    {
        get => _vramText;
        set => SetProperty(ref _vramText, value);
    }

    public string SharedVramText
    {
        get => _sharedVramText;
        set => SetProperty(ref _sharedVramText, value);
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
        DedicatedVramBytes = snapshot.DedicatedGpuMemoryBytes ?? 0;
        VramText = ByteFormatter.Format(snapshot.DedicatedGpuMemoryBytes);
        SharedVramText = ByteFormatter.Format(snapshot.SharedGpuMemoryBytes);
        RamText = ByteFormatter.Format(snapshot.WorkingSetBytes);
        CpuText = snapshot.CpuPercent.HasValue ? $"{snapshot.CpuPercent.Value:0}%" : "—";
        ExecutablePath = !string.IsNullOrWhiteSpace(snapshot.ExecutablePath) ? snapshot.ExecutablePath : "Unavailable";
        CommandLine = !string.IsNullOrWhiteSpace(snapshot.CommandLine) ? snapshot.CommandLine : "Unavailable";
    }
}
