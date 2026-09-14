using System.Collections.ObjectModel;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private string _systemRamText = "—";
    private double _systemRamPercent;
    private string? _gpuWarningMessage;
    private string? _processWarningMessage;

    private AiWorkloadViewModel? _selectedWorkload;
    private string? _selectedWorkloadId;

    private GpuProcessViewModel? _selectedOtherProcess;
    private int? _selectedOtherProcessPid;

    private GpuProcessViewModel? _inspectedMemberProcess;

    public ObservableCollection<GpuDeviceViewModel> Gpus { get; } = [];
    public ObservableCollection<AiWorkloadViewModel> Workloads { get; } = [];
    public ObservableCollection<GpuProcessViewModel> OtherGpuProcesses { get; } = [];

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

    public bool HasWorkloads => Workloads.Count > 0;
    public bool HasOtherGpuProcesses => OtherGpuProcesses.Count > 0;

    public AiWorkloadViewModel? SelectedWorkload
    {
        get => _selectedWorkload;
        set
        {
            if (SetProperty(ref _selectedWorkload, value))
            {
                _selectedWorkloadId = value?.WorkloadId;
                if (value != null)
                {
                    // Deselect other process
                    SelectedOtherProcess = null;
                    // Default inspected member to primary process
                    InspectedMemberProcess = value.MemberProcesses.FirstOrDefault(m => m.Pid == value.PrimaryPid)
                        ?? value.MemberProcesses.FirstOrDefault();
                }
                OnPropertyChanged(nameof(HasSelectedWorkload));
                OnPropertyChanged(nameof(HasAnySelection));
                OnPropertyChanged(nameof(ActiveProcessDetails));
                OnPropertyChanged(nameof(HasActiveProcessDetails));
            }
        }
    }

    public bool HasSelectedWorkload => SelectedWorkload is not null;

    public GpuProcessViewModel? SelectedOtherProcess
    {
        get => _selectedOtherProcess;
        set
        {
            if (SetProperty(ref _selectedOtherProcess, value))
            {
                _selectedOtherProcessPid = value?.Pid;
                if (value != null)
                {
                    SelectedWorkload = null;
                    InspectedMemberProcess = null;
                }
                OnPropertyChanged(nameof(HasSelectedOtherProcess));
                OnPropertyChanged(nameof(HasAnySelection));
                OnPropertyChanged(nameof(ActiveProcessDetails));
                OnPropertyChanged(nameof(HasActiveProcessDetails));
            }
        }
    }

    public bool HasSelectedOtherProcess => SelectedOtherProcess is not null;

    public GpuProcessViewModel? InspectedMemberProcess
    {
        get => _inspectedMemberProcess;
        set
        {
            if (SetProperty(ref _inspectedMemberProcess, value))
            {
                OnPropertyChanged(nameof(ActiveProcessDetails));
                OnPropertyChanged(nameof(HasActiveProcessDetails));
            }
        }
    }

    public GpuProcessViewModel? ActiveProcessDetails => SelectedOtherProcess ?? InspectedMemberProcess;
    public bool HasActiveProcessDetails => ActiveProcessDetails is not null;

    public bool HasAnySelection => HasSelectedWorkload || HasSelectedOtherProcess;

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

    public void UpdateSnapshot(AppSnapshot appSnapshot)
    {
        var snapshot = appSnapshot.Telemetry;

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

        // 4. Process lookup
        var processLookup = snapshot.GpuProcesses
            .GroupBy(p => p.Pid)
            .ToDictionary(g => g.Key, g => g.First());

        // 5. Reconcile Workloads
        var existingWorkloadMap = Workloads.ToDictionary(w => w.WorkloadId);
        var currentWorkloadIds = appSnapshot.Workloads.Select(w => w.WorkloadId).ToHashSet();

        for (int i = Workloads.Count - 1; i >= 0; i--)
        {
            if (!currentWorkloadIds.Contains(Workloads[i].WorkloadId))
            {
                Workloads.RemoveAt(i);
            }
        }

        for (int targetIndex = 0; targetIndex < appSnapshot.Workloads.Count; targetIndex++)
        {
            var snap = appSnapshot.Workloads[targetIndex];
            if (existingWorkloadMap.TryGetValue(snap.WorkloadId, out var vm))
            {
                vm.Update(snap, processLookup);
                int currentIndex = Workloads.IndexOf(vm);
                if (currentIndex != targetIndex && currentIndex >= 0)
                {
                    Workloads.Move(currentIndex, targetIndex);
                }
            }
            else
            {
                var newVm = new AiWorkloadViewModel(snap, processLookup);
                Workloads.Insert(targetIndex, newVm);
                existingWorkloadMap[snap.WorkloadId] = newVm;
            }
        }

        OnPropertyChanged(nameof(HasWorkloads));

        // Restore or clear SelectedWorkload
        if (_selectedWorkloadId != null)
        {
            var matched = Workloads.FirstOrDefault(w => w.WorkloadId == _selectedWorkloadId);
            if (matched != null)
            {
                _selectedWorkload = matched;
                OnPropertyChanged(nameof(SelectedWorkload));
                OnPropertyChanged(nameof(HasSelectedWorkload));
                OnPropertyChanged(nameof(HasAnySelection));

                if (InspectedMemberProcess != null)
                {
                    InspectedMemberProcess = matched.MemberProcesses.FirstOrDefault(m => m.Pid == InspectedMemberProcess.Pid)
                        ?? matched.MemberProcesses.FirstOrDefault();
                }
                else
                {
                    InspectedMemberProcess = matched.MemberProcesses.FirstOrDefault(m => m.Pid == matched.PrimaryPid)
                        ?? matched.MemberProcesses.FirstOrDefault();
                }
            }
            else
            {
                _selectedWorkload = null;
                _selectedWorkloadId = null;
                InspectedMemberProcess = null;
                OnPropertyChanged(nameof(SelectedWorkload));
                OnPropertyChanged(nameof(HasSelectedWorkload));
                OnPropertyChanged(nameof(HasAnySelection));
                OnPropertyChanged(nameof(ActiveProcessDetails));
                OnPropertyChanged(nameof(HasActiveProcessDetails));
            }
        }

        // 6. Reconcile Other GPU Processes
        var allWorkloadPids = appSnapshot.Workloads.SelectMany(w => w.ProcessPids).ToHashSet();
        var otherProcessesList = snapshot.GpuProcesses
            .Where(p => !allWorkloadPids.Contains(p.Pid))
            .OrderByDescending(p => p.LocalGpuMemoryBytes ?? 0)
            .ThenBy(p => p.Pid)
            .ToList();

        var existingOtherMap = OtherGpuProcesses.ToDictionary(p => p.Pid);
        var currentOtherPids = otherProcessesList.Select(p => p.Pid).ToHashSet();

        for (int i = OtherGpuProcesses.Count - 1; i >= 0; i--)
        {
            if (!currentOtherPids.Contains(OtherGpuProcesses[i].Pid))
            {
                OtherGpuProcesses.RemoveAt(i);
            }
        }

        for (int targetIndex = 0; targetIndex < otherProcessesList.Count; targetIndex++)
        {
            var procSnap = otherProcessesList[targetIndex];
            if (existingOtherMap.TryGetValue(procSnap.Pid, out var vm))
            {
                vm.UpdateFromSnapshot(procSnap, null);
                int currentIndex = OtherGpuProcesses.IndexOf(vm);
                if (currentIndex != targetIndex && currentIndex >= 0)
                {
                    OtherGpuProcesses.Move(currentIndex, targetIndex);
                }
            }
            else
            {
                var newVm = new GpuProcessViewModel();
                newVm.UpdateFromSnapshot(procSnap, null);
                OtherGpuProcesses.Insert(targetIndex, newVm);
                existingOtherMap[procSnap.Pid] = newVm;
            }
        }

        OnPropertyChanged(nameof(HasOtherGpuProcesses));

        // Restore or clear SelectedOtherProcess
        if (_selectedOtherProcessPid != null)
        {
            var matchedProc = OtherGpuProcesses.FirstOrDefault(p => p.Pid == _selectedOtherProcessPid.Value);
            if (matchedProc != null)
            {
                _selectedOtherProcess = matchedProc;
                OnPropertyChanged(nameof(SelectedOtherProcess));
                OnPropertyChanged(nameof(HasSelectedOtherProcess));
                OnPropertyChanged(nameof(HasAnySelection));
                OnPropertyChanged(nameof(ActiveProcessDetails));
                OnPropertyChanged(nameof(HasActiveProcessDetails));
            }
            else
            {
                _selectedOtherProcess = null;
                _selectedOtherProcessPid = null;
                OnPropertyChanged(nameof(SelectedOtherProcess));
                OnPropertyChanged(nameof(HasSelectedOtherProcess));
                OnPropertyChanged(nameof(HasAnySelection));
                OnPropertyChanged(nameof(ActiveProcessDetails));
                OnPropertyChanged(nameof(HasActiveProcessDetails));
            }
        }
    }
}
