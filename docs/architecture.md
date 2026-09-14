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
LocalAITaskManager.Windows (P/Invoke, NVML, PDH, Win32 RAM, Process ancestry, Workload Detectors)
         │
         ▼
LocalAITaskManager.Core (Domain entities, abstractions, formatters, pure logic)
```

1. **`LocalAITaskManager.Core`**:
   - Zero Windows dependencies (cross-platform compatible domain and tests).
   - Contains immutable domain snapshots (`SystemSnapshot`, `DetectionSnapshot`, `AiProcessIdentity`, `DetectedModelIdentity`, `AiWorkloadSnapshot`, `AppSnapshot`).
   - Abstractions: `IGpuTelemetryProvider`, `IGpuProcessMemoryProvider`, `ISystemMemoryProvider`, `IProcessInfoProvider`, `ISystemSnapshotProvider`, `IProcessRelationshipProvider`, `ICommandLineParser`, `IRuntimeDetector`, `IWorkloadDetectionCoordinator`, `IAiWorkloadComposer`.
   - Pure math and logic: `AiWorkloadComposer`, `ProcessCpuCalculator`, `GpuProcessMemoryAggregator`, `ByteFormatter`, `GgufQuantizationInference`.

2. **`LocalAITaskManager.Windows`**:
   - Interacts with operating system and hardware drivers directly.
   - P/Invoke bindings for NVIDIA Management Library (NVML) and Performance Data Helper (`pdh.dll`).
   - Win32 memory queries via `GlobalMemoryStatusEx`.
   - Process hierarchy inspection via `CreateToolhelp32Snapshot` (`Process32FirstW` / `Process32NextW`).
   - Command line parsing using native `CommandLineToArgvW`.
   - Runtime-specific detectors:
     - `OllamaRuntimeDetector` (HTTP loopback query to `/api/ps` with runner PID set cache invalidation).
     - `LmStudioRuntimeDetector` (process ancestry and executable path matching; model correlation deactivated/unvalidated per `UNKNOWN > WRONG`).
     - `LlamaCppRuntimeDetector` (command line tokenizer; `High` confidence with structural flags, `Medium` for executable-only).
   - `WorkloadDetectionCoordinator`: coordinates detectors and enforces strict precedence rules (`Ollama` / `LM Studio` > generic `llama.cpp` fallback).

3. **`LocalAITaskManager.App`**:
   - WPF application providing MVVM view models (`MainViewModel`, `AiWorkloadViewModel`, `GpuProcessViewModel`, `GpuDeviceViewModel`).
   - Keyed in-place collection reconciliation preserving selection and scroll state without UI flicker.
   - Dual inspection hierarchy (Workload level with member process breakdown; standalone process level).
   - Background sampling loop running at ~1 Hz using `PeriodicTimer`.
   - Threading isolation: collectors, detectors, and composer run asynchronously in the background, never blocking the UI thread.
   - Dispatcher synchronization publishes immutable snapshots to observable view models.

## Telemetry & Workload Composition Flow

```text
NVML ──┐
PDH  ──┼─► SystemSnapshot ──┐
Win32 ─┘                    │
                            ▼
              WorkloadDetectionCoordinator ──► DetectionSnapshot ──┐
                (llama.cpp, Ollama, LMS)                           │
                                                                   ▼
                                                           AiWorkloadComposer
                                                                   │
                                                                   ▼
                                                              AppSnapshot
                                                                   │
                                                                   ▼
                                                             MainViewModel
                                                            (Reconciliation)
                                                                   │
                                                                   ▼
                                                               WPF View
                                                       (Workloads + Inspector)
```

## AI Workload Composition Rules

The `AiWorkloadComposer` transforms raw detected processes into first-class user-facing workloads according to deterministic domain rules:

1. **Grouping Key:** Processes detected with AI runtime identities are grouped by `(Runtime, RuntimeRootPid ?? Pid)`.
2. **Composition Semantics:**
   - **Single Model (Case 1):** If the group contains exactly 1 model assignment, a single `Model` workload is generated containing both the controller process and runner process. The primary PID is set to the runner executing the model, and primary VRAM is the runner's WDDM Local Usage.
   - **Multi-Model (Case 2):** If the group contains multiple loaded models / runner processes (e.g. concurrent Ollama models), each runner becomes a distinct `Model` workload. The shared controller/service is separated into a dedicated `RuntimeService` workload.
   - **Runtime Only (Case 3):** If no models are detected (e.g. idle Ollama service or LM Studio), a `RuntimeOnly` workload is generated. The primary PID is selected by root PID with GPU memory > largest GPU consumer > lowest PID.
3. **Unique PID Invariant:** Every process PID belongs to at most one workload (`ProcessPids` sets across all workloads are strictly disjoint).
4. **Other GPU Processes:** All active GPU processes not attributed to an AI workload are automatically segregated into `OTHER GPU PROCESSES`.
5. **Deterministic WorkloadId:** Workload IDs are purely deterministic strings (e.g. `ollama:model:qwen2.5:1.5b`, `llama.cpp:model:46588`, `lmstudio:runtime:1234`) enabling stable keyed reconciliation in the UI.

## Source-of-Truth Hierarchy

### Ollama
* **Runtime Identity:** Process ancestry (descendant of `ollama.exe`), executable path (`...\Ollama\lib\...`), or direct executable match.
* **Model Metadata:** Official loopback REST endpoint `GET http://127.0.0.1:11434/api/ps` (cache invalidated instantly upon runner PID set change).
* **GPU Memory:** Windows WDDM Local Usage.

### LM Studio
* **Runtime Identity:** Process ancestry, executable path (`...\LM Studio\...`), or direct executable match.
* **Loaded Model:** *Not validated / disabled* (model unassigned until daemon-less or verified schema is available).
* **GPU Memory:** Windows WDDM Local Usage.

### Standalone llama.cpp
* **Runtime Identity:** Executable name (`llama-server.exe`, `llama-cli.exe`) + structural command line flags.
* **Model:** `-m` / `--model` command line parameters; filename-based quantization inference.
* **Context:** `-c` / `--ctx-size` command line parameters.
* **GPU Memory:** Windows WDDM Local Usage.

## Precedence & The "UNKNOWN > WRONG" Principle

1. **Precedence:** `llama-server.exe` can be used as a backend for multiple runtimes (e.g. Ollama or standalone). `WorkloadDetectionCoordinator` prioritizes specific runtime evidence (`Ollama`, `LM Studio`) with `Confirmed` confidence over generic `llama.cpp` detection with `High` confidence. An Ollama runner process is never incorrectly classified as standalone `llama.cpp`.
2. **Ambiguity Handling:** When Ollama reports multiple loaded models and multiple runner processes exist without a deterministic 1:1 mapping, the coordinator identifies the processes as Ollama runners while setting `Model = null` and preserving the models in `UnmappedModels`.
3. **Non-AI Workloads:** Generic processes using GPU memory (such as `python.exe` or `dwm.exe`) without verifiable AI evidence remain classified as `Unknown` (`—`) to prevent false attribution.

## Why Process VRAM Does Not Use NVML Under WDDM

On Windows desktop systems using standard GeForce and RTX drivers, the GPU operates under the **Windows Display Driver Model (WDDM)**. Under WDDM, the Windows Kernel Mode Driver (`dxgkrnl.sys` / KMD) is the authoritative arbiter of all virtual memory and video memory allocations.

As officially documented in NVIDIA's NVML API documentation:
> Under Windows WDDM mode, `nvmlProcessInfo_t.usedGpuMemory` is reported as `NVML_VALUE_NOT_AVAILABLE` because memory management is handled by Windows KMD.

To obtain accurate, per-process GPU memory without requiring elevated privileges, **Local AI Task Manager** queries the Windows Performance Data Helper (PDH) counter set `\GPU Process Memory(*)`:
* **`Local Usage` (Primary process "VRAM" metric):** Memory currently resident on the local video memory of the GPU adapter.
* **`Non Local Usage`:** Memory allocated by or on behalf of the process residing outside the GPU adapter's local memory (e.g. system RAM).
* **`Total Committed`:** Total virtual video memory currently committed by the video memory manager for this process.
* **`Dedicated Usage`:** Dedicated memory allocated across the process lifetime.
* **`Shared Usage`:** System memory shared with the GPU.
