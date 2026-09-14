using System.Collections.ObjectModel;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private string _systemRamText = "—";
    private double _systemRamPercent;
    private GpuProcessViewModel? _selectedProcess;
    private string? _gpuWarningMessage;
    private string? _processWarningMessage;

    public ObservableCollection<GpuDeviceViewModel> Gpus { get; } = [];
    public ObservableCollection<GpuProcessViewModel> Processes { get; } = [];

    public string SystemRamText
    {
        get => _systemRamText;
        set => SetProperty(ref _systemRamText, value);
    }

    public double SystemRamPercent
    {
        get => _systemRamPercent;
        set => SetProperty(ref _systemRamPercent, value);
    }

    public GpuProcessViewModel? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (SetProperty(ref _selectedProcess, value))
            {
                OnPropertyChanged(nameof(HasSelectedProcess));
            }
        }
    }

    public bool HasSelectedProcess => SelectedProcess is not null;

    public string? GpuWarningMessage
    {
        get => _gpuWarningMessage;
        set
        {
            if (SetProperty(ref _gpuWarningMessage, value))
            {
                OnPropertyChanged(nameof(HasGpuWarning));
            }
        }
    }

    public bool HasGpuWarning => !string.IsNullOrEmpty(GpuWarningMessage);

    public string? ProcessWarningMessage
    {
        get => _processWarningMessage;
        set
        {
            if (SetProperty(ref _processWarningMessage, value))
            {
                OnPropertyChanged(nameof(HasProcessWarning));
            }
        }
    }

    public bool HasProcessWarning => !string.IsNullOrEmpty(ProcessWarningMessage);

    public void UpdateSnapshot(SystemSnapshot snapshot, DetectionSnapshot? detection = null)
    {
        // 1. Warnings
        string? gpuWarn = snapshot.Warnings.FirstOrDefault(w => w.Source == "NVIDIA")?.Message;
        GpuWarningMessage = gpuWarn;

        string? pdhWarn = snapshot.Warnings.FirstOrDefault(w => w.Source == "PDH")?.Message;
        ProcessWarningMessage = pdhWarn;

        // 2. GPUs
        if (snapshot.Gpus.Count == 0 && string.IsNullOrEmpty(gpuWarn))
        {
            GpuWarningMessage = "No supported NVIDIA GPU detected.";
        }

        while (Gpus.Count < snapshot.Gpus.Count)
        {
            Gpus.Add(new GpuDeviceViewModel());
        }
        while (Gpus.Count > snapshot.Gpus.Count)
        {
            Gpus.RemoveAt(Gpus.Count - 1);
        }
        for (int i = 0; i < snapshot.Gpus.Count; i++)
        {
            Gpus[i].UpdateFromSnapshot(snapshot.Gpus[i]);
        }

        // 3. System RAM
        SystemRamText = ByteFormatter.FormatRatio(snapshot.Memory.UsedPhysicalBytes, snapshot.Memory.TotalPhysicalBytes);
        SystemRamPercent = snapshot.Memory.TotalPhysicalBytes is > 0 && snapshot.Memory.UsedPhysicalBytes.HasValue
            ? Math.Clamp((double)snapshot.Memory.UsedPhysicalBytes.Value / snapshot.Memory.TotalPhysicalBytes.Value * 100.0, 0.0, 100.0)
            : 0.0;

        // 4. Processes
        int? selectedPid = SelectedProcess?.Pid;

        var existingMap = Processes.ToDictionary(p => p.Pid);
        var updatedList = new List<GpuProcessViewModel>(snapshot.GpuProcesses.Count);

        foreach (var procSnapshot in snapshot.GpuProcesses)
        {
            AiProcessIdentity? aiIdentity = null;
            detection?.Processes.TryGetValue(procSnapshot.Pid, out aiIdentity);

            if (existingMap.TryGetValue(procSnapshot.Pid, out var vm))
            {
                vm.UpdateFromSnapshot(procSnapshot, aiIdentity);
                updatedList.Add(vm);
            }
            else
            {
                var newVm = new GpuProcessViewModel();
                newVm.UpdateFromSnapshot(procSnapshot, aiIdentity);
                updatedList.Add(newVm);
            }
        }

        Processes.Clear();
        foreach (var item in updatedList)
        {
            Processes.Add(item);
        }

        if (selectedPid.HasValue)
        {
            SelectedProcess = Processes.FirstOrDefault(p => p.Pid == selectedPid.Value);
        }
    }
}
