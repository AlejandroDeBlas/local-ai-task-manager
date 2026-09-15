using LocalAITaskManager.App.ViewModels;
using LocalAITaskManager.Core.Models;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class MainViewModelReconciliationTests
{
    private static GpuProcessSnapshot CreateProcess(int pid, string name, ulong? localGpu = 1000000000, ulong? ram = 500000000, double? cpu = 2.5)
    {
        return new GpuProcessSnapshot(
            Pid: pid,
            ProcessName: name,
            LocalGpuMemoryBytes: localGpu,
            NonLocalGpuMemoryBytes: 0,
            TotalCommittedGpuMemoryBytes: localGpu,
            DedicatedGpuMemoryBytes: localGpu,
            SharedGpuMemoryBytes: 0,
            WorkingSetBytes: ram,
            CpuPercent: cpu,
            ExecutablePath: $@"C:\Tools\{name}",
            CommandLine: name
        );
    }

    private static SystemSnapshot CreateTelemetry(params GpuProcessSnapshot[] procs)
    {
        return new SystemSnapshot(
            Timestamp: DateTimeOffset.UtcNow,
            Gpus: [],
            Memory: new SystemMemorySnapshot(null, null, null),
            GpuProcesses: procs,
            Warnings: []
        );
    }

    private static DetectionSnapshot CreateDetection(params AiProcessIdentity[] identities)
    {
        return new DetectionSnapshot(
            Timestamp: DateTimeOffset.UtcNow,
            Processes: identities.ToDictionary(i => i.Pid),
            UnmappedModels: [],
            Warnings: []
        );
    }

    [Fact]
    public void SelectedWorkload_Preserved_When_PrimaryPid_Changes_Under_Same_WorkloadId()
    {
        var vm = new MainViewModel();

        // Cycle 1: LM Studio with root 400 (50MB) and child 401 (2GB). Primary PID is 401.
        var p400_c1 = CreateProcess(400, "LM Studio.exe", localGpu: 50000000);
        var p401_c1 = CreateProcess(401, "child.exe", localGpu: 2000000000);
        var telem1 = CreateTelemetry(p400_c1, p401_c1);
        var det1 = CreateDetection(
            new AiProcessIdentity(400, AiRuntimeKind.LmStudio, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 400),
            new AiProcessIdentity(401, AiRuntimeKind.LmStudio, DetectionConfidence.Medium, null, DetectionConfidence.None, [], 400)
        );

        var workload1 = new AiWorkloadSnapshot(
            WorkloadId: "lmstudio:runtime:400",
            Kind: AiWorkloadKind.RuntimeOnly,
            Runtime: AiRuntimeKind.LmStudio,
            DisplayName: "LM Studio",
            PrimaryPid: 401,
            RuntimeRootPid: 400,
            Model: null,
            RuntimeConfidence: DetectionConfidence.Confirmed,
            ModelConfidence: DetectionConfidence.None,
            ProcessPids: [400, 401],
            PrimaryLocalGpuMemoryBytes: 2000000000,
            PrimaryWorkingSetBytes: 500000000,
            PrimaryCpuPercent: 2.5,
            Evidence: []
        );

        var appSnap1 = new AppSnapshot(telem1, det1, [workload1]);
        vm.UpdateSnapshot(appSnap1);

        Assert.Single(vm.Workloads);
        var originalWorkloadVm = vm.Workloads[0];

        // Select the workload
        vm.SelectedWorkload = originalWorkloadVm;
        Assert.True(vm.HasSelectedWorkload);
        Assert.Equal("lmstudio:runtime:400", vm.SelectedWorkload.WorkloadId);
        Assert.Equal(401, vm.SelectedWorkload.PrimaryPid);
        Assert.NotNull(vm.InspectedMemberProcess);
        Assert.Equal(401, vm.InspectedMemberProcess.Pid);

        // Cycle 2: Primary PID changes from 401 to 400 because 401 dropped VRAM
        var p400_c2 = CreateProcess(400, "LM Studio.exe", localGpu: 50000000);
        var p401_c2 = CreateProcess(401, "child.exe", localGpu: 10000000);
        var telem2 = CreateTelemetry(p400_c2, p401_c2);

        var workload2 = new AiWorkloadSnapshot(
            WorkloadId: "lmstudio:runtime:400",
            Kind: AiWorkloadKind.RuntimeOnly,
            Runtime: AiRuntimeKind.LmStudio,
            DisplayName: "LM Studio",
            PrimaryPid: 400,
            RuntimeRootPid: 400,
            Model: null,
            RuntimeConfidence: DetectionConfidence.Confirmed,
            ModelConfidence: DetectionConfidence.None,
            ProcessPids: [400, 401],
            PrimaryLocalGpuMemoryBytes: 50000000,
            PrimaryWorkingSetBytes: 500000000,
            PrimaryCpuPercent: 2.5,
            Evidence: []
        );

        var appSnap2 = new AppSnapshot(telem2, det1, [workload2]);
        vm.UpdateSnapshot(appSnap2);

        // Verify selection was preserved and points to the same VM instance
        Assert.True(vm.HasSelectedWorkload);
        Assert.Same(originalWorkloadVm, vm.SelectedWorkload);
        Assert.Equal(400, vm.SelectedWorkload.PrimaryPid);
    }

    [Fact]
    public void InspectedMemberProcess_Preserved_When_PrimaryPid_Changes()
    {
        var vm = new MainViewModel();

        var p400 = CreateProcess(400, "LM Studio.exe", localGpu: 50000000);
        var p401 = CreateProcess(401, "child1.exe", localGpu: 2000000000);
        var p402 = CreateProcess(402, "child2.exe", localGpu: 1000000000);
        var telem1 = CreateTelemetry(p400, p401, p402);
        var det = CreateDetection(
            new AiProcessIdentity(400, AiRuntimeKind.LmStudio, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 400),
            new AiProcessIdentity(401, AiRuntimeKind.LmStudio, DetectionConfidence.Medium, null, DetectionConfidence.None, [], 400),
            new AiProcessIdentity(402, AiRuntimeKind.LmStudio, DetectionConfidence.Medium, null, DetectionConfidence.None, [], 400)
        );

        var workload1 = new AiWorkloadSnapshot(
            WorkloadId: "lmstudio:runtime:400",
            Kind: AiWorkloadKind.RuntimeOnly,
            Runtime: AiRuntimeKind.LmStudio,
            DisplayName: "LM Studio",
            PrimaryPid: 401,
            RuntimeRootPid: 400,
            Model: null,
            RuntimeConfidence: DetectionConfidence.Confirmed,
            ModelConfidence: DetectionConfidence.None,
            ProcessPids: [400, 401, 402],
            PrimaryLocalGpuMemoryBytes: 2000000000,
            PrimaryWorkingSetBytes: 500000000,
            PrimaryCpuPercent: 2.5,
            Evidence: []
        );

        vm.UpdateSnapshot(new AppSnapshot(telem1, det, [workload1]));
        vm.SelectedWorkload = vm.Workloads[0];

        // User manually selects process 402 to inspect
        var member402 = vm.SelectedWorkload.MemberProcesses.First(m => m.Pid == 402);
        vm.InspectedMemberProcess = member402;
        Assert.Equal(402, vm.InspectedMemberProcess.Pid);

        // Cycle 2: Primary PID changes to 400, but 402 is still running
        var workload2 = new AiWorkloadSnapshot(
            WorkloadId: "lmstudio:runtime:400",
            Kind: AiWorkloadKind.RuntimeOnly,
            Runtime: AiRuntimeKind.LmStudio,
            DisplayName: "LM Studio",
            PrimaryPid: 400,
            RuntimeRootPid: 400,
            Model: null,
            RuntimeConfidence: DetectionConfidence.Confirmed,
            ModelConfidence: DetectionConfidence.None,
            ProcessPids: [400, 401, 402],
            PrimaryLocalGpuMemoryBytes: 50000000,
            PrimaryWorkingSetBytes: 500000000,
            PrimaryCpuPercent: 2.5,
            Evidence: []
        );

        vm.UpdateSnapshot(new AppSnapshot(telem1, det, [workload2]));

        // The user's inspection of process 402 remains untouched
        Assert.NotNull(vm.InspectedMemberProcess);
        Assert.Equal(402, vm.InspectedMemberProcess.Pid);
    }

    [Fact]
    public void InspectedMemberProcess_FallsBack_To_PrimaryPid_When_InspectedProcessExits()
    {
        var vm = new MainViewModel();

        var p400 = CreateProcess(400, "LM Studio.exe", localGpu: 50000000);
        var p401 = CreateProcess(401, "child1.exe", localGpu: 2000000000);
        var p402 = CreateProcess(402, "child2.exe", localGpu: 1000000000);
        var telem1 = CreateTelemetry(p400, p401, p402);
        var det = CreateDetection(
            new AiProcessIdentity(400, AiRuntimeKind.LmStudio, DetectionConfidence.Confirmed, null, DetectionConfidence.None, [], 400),
            new AiProcessIdentity(401, AiRuntimeKind.LmStudio, DetectionConfidence.Medium, null, DetectionConfidence.None, [], 400),
            new AiProcessIdentity(402, AiRuntimeKind.LmStudio, DetectionConfidence.Medium, null, DetectionConfidence.None, [], 400)
        );

        var workload1 = new AiWorkloadSnapshot(
            WorkloadId: "lmstudio:runtime:400",
            Kind: AiWorkloadKind.RuntimeOnly,
            Runtime: AiRuntimeKind.LmStudio,
            DisplayName: "LM Studio",
            PrimaryPid: 401,
            RuntimeRootPid: 400,
            Model: null,
            RuntimeConfidence: DetectionConfidence.Confirmed,
            ModelConfidence: DetectionConfidence.None,
            ProcessPids: [400, 401, 402],
            PrimaryLocalGpuMemoryBytes: 2000000000,
            PrimaryWorkingSetBytes: 500000000,
            PrimaryCpuPercent: 2.5,
            Evidence: []
        );

        vm.UpdateSnapshot(new AppSnapshot(telem1, det, [workload1]));
        vm.SelectedWorkload = vm.Workloads[0];

        // User was inspecting PID 402
        vm.InspectedMemberProcess = vm.SelectedWorkload.MemberProcesses.First(m => m.Pid == 402);
        Assert.Equal(402, vm.InspectedMemberProcess.Pid);

        // Cycle 2: Process 402 exits, leaving 400 and 401. PrimaryPid is 401.
        var telem2 = CreateTelemetry(p400, p401);
        var workload2 = new AiWorkloadSnapshot(
            WorkloadId: "lmstudio:runtime:400",
            Kind: AiWorkloadKind.RuntimeOnly,
            Runtime: AiRuntimeKind.LmStudio,
            DisplayName: "LM Studio",
            PrimaryPid: 401,
            RuntimeRootPid: 400,
            Model: null,
            RuntimeConfidence: DetectionConfidence.Confirmed,
            ModelConfidence: DetectionConfidence.None,
            ProcessPids: [400, 401],
            PrimaryLocalGpuMemoryBytes: 2000000000,
            PrimaryWorkingSetBytes: 500000000,
            PrimaryCpuPercent: 2.5,
            Evidence: []
        );

        vm.UpdateSnapshot(new AppSnapshot(telem2, det, [workload2]));

        // Fallback switches to PrimaryPid (401)
        Assert.NotNull(vm.InspectedMemberProcess);
        Assert.Equal(401, vm.InspectedMemberProcess.Pid);
    }
}
