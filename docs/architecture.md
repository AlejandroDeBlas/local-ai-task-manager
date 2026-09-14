# Architecture Overview

## Technology Stack

* **Platform:** Windows 10 / Windows 11 (x64)
* **Framework:** .NET 10 LTS
* **UI Framework:** Windows Presentation Foundation (WPF) with pure XAML dark theme
* **Language:** C# 13

## Layering & Separation of Concerns

```text
LocalAITaskManager.App (WPF GUI, ViewModels, Dispatcher orchestration)
         │
         ▼
LocalAITaskManager.Windows (P/Invoke, NVML, PDH, Win32 RAM, WMI Process metadata)
         │
         ▼
LocalAITaskManager.Core (Domain entities, abstractions, formatters, pure logic)
```

1. **`LocalAITaskManager.Core`**:
   - Zero Windows dependencies (can be referenced by cross-platform libraries/tests).
   - Contains immutable domain snapshots (`SystemSnapshot`, `GpuDeviceSnapshot`, `GpuProcessSnapshot`).
   - Abstractions: `IGpuTelemetryProvider`, `IGpuProcessMemoryProvider`, `ISystemMemoryProvider`, `IProcessInfoProvider`, `ISystemSnapshotProvider`.
   - Pure math and logic: `ProcessCpuCalculator`, `GpuProcessMemoryAggregator`, `ByteFormatter`.

2. **`LocalAITaskManager.Windows`**:
   - Interacts with operating system and hardware drivers directly.
   - P/Invoke bindings for NVIDIA Management Library (NVML) and Performance Data Helper (`pdh.dll`).
   - Win32 memory queries via `GlobalMemoryStatusEx`.
   - Process image inspection via `OpenProcess` with `PROCESS_QUERY_LIMITED_INFORMATION` and `QueryFullProcessImageNameW`.
   - Command line inspection via WMI `Win32_Process.CommandLine` with strict PID-based caching.

3. **`LocalAITaskManager.App`**:
   - WPF application providing MVVM view models and views.
   - Background sampling loop running at ~1 Hz using `PeriodicTimer`.
   - Threading isolation: collectors run asynchronously in the background, never blocking the UI thread.
   - Dispatcher synchronization publishes immutable snapshots to observable view models.

## Telemetry Flow

```text
NVML (Global GPU, VRAM, temp, power) ───┐
PDH (Per-process dedicated/shared VRAM) ─┤
Win32 RAM (Physical total/used memory) ──┼──> WindowsSystemSnapshotProvider
Process APIs (Working set, CPU time) ───┤               │
WMI metadata (Cached command line) ─────┘               ▼
                                                  SystemSnapshot (Immutable)
                                                        │
                                                        ▼
                                                  MainViewModel
                                                        │
                                                        ▼
                                                    WPF View
```

## Why Process VRAM Does Not Use NVML Under WDDM

On Windows desktop systems using standard GeForce and RTX drivers, the GPU operates under the **Windows Display Driver Model (WDDM)**. Under WDDM, the Windows Kernel Mode Driver (`dxgkrnl.sys` / KMD) is the authoritative arbiter of all virtual memory and video memory allocations.

As officially documented in NVIDIA's NVML API documentation:
> Under Windows WDDM mode, `nvmlProcessInfo_t.usedGpuMemory` is reported as `NVML_VALUE_NOT_AVAILABLE` because memory management is handled by Windows KMD.

Consequently, querying `nvmlDeviceGetComputeRunningProcesses` or `nvmlDeviceGetGraphicsRunningProcesses` yields `usedGpuMemory = NVML_VALUE_NOT_AVAILABLE` for process memory allocations.

To obtain accurate, per-process dedicated and shared GPU memory without requiring elevated privileges, **Local AI Task Manager** uses the Windows Performance Data Helper (PDH) querying:
* `\GPU Process Memory(*)\Dedicated Usage`
* `\GPU Process Memory(*)\Shared Usage`

Instance strings (e.g. `pid_18420_luid_0x00000000_0x0000ABCD_phys_0`) are parsed using `GpuProcessCounterInstanceParser` and aggregated by PID across physical adapters.

## Process Metadata & CPU Sampling

* **Command Line Caching:** Querying WMI `Win32_Process` is computationally expensive. The provider maintains a thread-safe cache by PID. When a PID is observed with GPU memory for the first time, its command line is queried once. The entry is maintained until the PID disappears from the snapshot.
* **CPU Utilization:** Win32 does not provide an instantaneous CPU% per process. Instead, `Process.TotalProcessorTime` is sampled across intervals. The CPU usage is computed as:
  $$\text{CPU \%} = \frac{\Delta \text{TotalProcessorTime}}{\Delta \text{WallClock} \times \text{Environment.ProcessorCount}} \times 100$$
  The first sample yields `null` to avoid fabricating metrics. Dead processes have their state purged automatically.
