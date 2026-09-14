using System.Diagnostics;
using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Models;
using LocalAITaskManager.Core.Services;
using LocalAITaskManager.Windows.Nvidia;
using LocalAITaskManager.Windows.PerformanceCounters;

namespace LocalAITaskManager.Windows.Telemetry;

public sealed class WindowsSystemSnapshotProvider : ISystemSnapshotProvider
{
    private readonly IGpuTelemetryProvider _gpuProvider;
    private readonly IGpuProcessMemoryProvider _processMemoryProvider;
    private readonly ISystemMemoryProvider _memoryProvider;
    private readonly IProcessInfoProvider _processInfoProvider;
    private readonly IProcessCpuSampler _cpuSampler;

    public WindowsSystemSnapshotProvider(
        IGpuTelemetryProvider gpuProvider,
        IGpuProcessMemoryProvider processMemoryProvider,
        ISystemMemoryProvider memoryProvider,
        IProcessInfoProvider processInfoProvider,
        IProcessCpuSampler cpuSampler)
    {
        _gpuProvider = gpuProvider ?? throw new ArgumentNullException(nameof(gpuProvider));
        _processMemoryProvider = processMemoryProvider ?? throw new ArgumentNullException(nameof(processMemoryProvider));
        _memoryProvider = memoryProvider ?? throw new ArgumentNullException(nameof(memoryProvider));
        _processInfoProvider = processInfoProvider ?? throw new ArgumentNullException(nameof(processInfoProvider));
        _cpuSampler = cpuSampler ?? throw new ArgumentNullException(nameof(cpuSampler));
    }

    public async Task<SystemSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        var warnings = new List<TelemetryWarning>();

        // 1. GPU Telemetry
        IReadOnlyList<GpuDeviceSnapshot> gpus = [];
        try
        {
            gpus = await _gpuProvider.GetGpusAsync(cancellationToken);
            if (gpus.Count == 0)
            {
                string message = "No supported NVIDIA GPU detected.";
                if (_gpuProvider is NvmlGpuTelemetryProvider nvml && !string.IsNullOrWhiteSpace(nvml.InitializationError))
                {
                    message = $"NVIDIA telemetry unavailable: {nvml.InitializationError}";
                }
                warnings.Add(new TelemetryWarning("NVIDIA", message, timestamp));
            }
        }
        catch (Exception ex)
        {
            warnings.Add(new TelemetryWarning("NVIDIA", $"NVIDIA telemetry error: {ex.Message}", timestamp));
        }

        // 2. System Memory
        SystemMemorySnapshot memory;
        try
        {
            memory = _memoryProvider.GetSnapshot();
        }
        catch (Exception ex)
        {
            memory = new SystemMemorySnapshot(0, 0, 0);
            warnings.Add(new TelemetryWarning("Memory", $"System memory query error: {ex.Message}", timestamp));
        }

        // 3. Process GPU Memory (PDH)
        IReadOnlyList<GpuProcessMemorySample> memorySamples = [];
        bool pdhFailed = false;
        try
        {
            memorySamples = await _processMemoryProvider.GetProcessMemoryAsync(cancellationToken);
            if (_processMemoryProvider is PdhGpuProcessMemoryProvider pdh && !string.IsNullOrWhiteSpace(pdh.LastError))
            {
                pdhFailed = true;
                warnings.Add(new TelemetryWarning("PDH", $"GPU process telemetry unavailable: {pdh.LastError}", timestamp));
            }
        }
        catch (Exception ex)
        {
            pdhFailed = true;
            warnings.Add(new TelemetryWarning("PDH", $"GPU process telemetry unavailable: {ex.Message}", timestamp));
        }

        // 4. Process aggregation & inspection
        var processSnapshots = new List<GpuProcessSnapshot>();

        if (!pdhFailed)
        {
            var aggregated = GpuProcessMemoryAggregator.AggregateByPid(memorySamples);
            var activePids = new HashSet<int>(aggregated.Select(a => a.Pid));

            foreach (var item in aggregated)
            {
                if (item.DedicatedBytes == 0 && item.SharedBytes == 0)
                {
                    continue;
                }

                ulong workingSet = 0;
                TimeSpan? totalProcessorTime = null;

                try
                {
                    using var proc = Process.GetProcessById(item.Pid);
                    workingSet = (ulong)Math.Max(0, proc.WorkingSet64);
                    totalProcessorTime = proc.TotalProcessorTime;
                }
                catch
                {
                    // Process exited between query and sampling
                }

                ProcessMetadata metadata = _processInfoProvider.GetMetadata(item.Pid);
                double? cpu = totalProcessorTime.HasValue
                    ? _cpuSampler.SampleCpu(item.Pid, totalProcessorTime.Value, timestamp)
                    : null;

                processSnapshots.Add(new GpuProcessSnapshot(
                    Pid: item.Pid,
                    ProcessName: metadata.ProcessName,
                    ExecutablePath: metadata.ExecutablePath,
                    CommandLine: metadata.CommandLine,
                    WorkingSetBytes: workingSet,
                    CpuPercent: cpu,
                    DedicatedGpuMemoryBytes: item.DedicatedBytes,
                    SharedGpuMemoryBytes: item.SharedBytes
                ));
            }

            // Cleanup caches
            _processInfoProvider.Cleanup(activePids);
            _cpuSampler.Cleanup(activePids);

            // Sort by Dedicated GPU Memory descending
            processSnapshots.Sort((a, b) =>
                (b.DedicatedGpuMemoryBytes ?? 0).CompareTo(a.DedicatedGpuMemoryBytes ?? 0));
        }

        return new SystemSnapshot(
            Timestamp: timestamp,
            Gpus: gpus,
            Memory: memory,
            GpuProcesses: processSnapshots,
            Warnings: warnings
        );
    }
}
