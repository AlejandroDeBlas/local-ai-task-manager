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
   - Zero Windows dependencies (can be referenced by cross-platform libraries/tests).
   - Contains immutable domain snapshots (`SystemSnapshot`, `DetectionSnapshot`, `AiProcessIdentity`, `DetectedModelIdentity`).
   - Abstractions: `IGpuTelemetryProvider`, `IGpuProcessMemoryProvider`, `ISystemMemoryProvider`, `IProcessInfoProvider`, `ISystemSnapshotProvider`, `IProcessRelationshipProvider`, `ICommandLineParser`, `ILocalCommandRunner`, `IRuntimeDetector`, `IWorkloadDetectionCoordinator`.
   - Pure math and logic: `ProcessCpuCalculator`, `GpuProcessMemoryAggregator`, `ByteFormatter`, `GgufQuantizationInference`.

2. **`LocalAITaskManager.Windows`**:
   - Interacts with operating system and hardware drivers directly.
   - P/Invoke bindings for NVIDIA Management Library (NVML) and Performance Data Helper (`pdh.dll`).
   - Win32 memory queries via `GlobalMemoryStatusEx`.
   - Process hierarchy inspection via `CreateToolhelp32Snapshot` (`Process32FirstW` / `Process32NextW`).
   - Command line parsing using native `CommandLineToArgvW`.
   - Runtime-specific detectors:
     - `OllamaRuntimeDetector` (HTTP loopback query to `/api/ps` with local caching).
     - `LmStudioRuntimeDetector` (secure subprocess invocation of `lms ps --json` with local caching).
     - `LlamaCppRuntimeDetector` (command line tokenizer for `-m`, `--model`, `-c`, `--models-dir`).
   - `WorkloadDetectionCoordinator`: coordinates detectors and enforces strict precedence rules (`Ollama` / `LM Studio` > generic `llama.cpp` fallback).

3. **`LocalAITaskManager.App`**:
   - WPF application providing MVVM view models and views.
   - Background sampling loop running at ~1 Hz using `PeriodicTimer`.
   - Threading isolation: collectors and detectors run asynchronously in the background, never blocking the UI thread.
   - Dispatcher synchronization publishes immutable snapshots to observable view models.

## Telemetry & Detection Flow

```text
                    ┌──────────────┐
NVML ───────────────►              │
PDH ────────────────► System       │
Win32 ──────────────► Snapshot     │
                    └──────┬───────┘
                           │
                           ▼
               WorkloadDetectionCoordinator
                 │         │          │
                 ▼         ▼          ▼
             llama.cpp   Ollama    LM Studio
              detector   detector    detector
                 │         │          │
                 └─────────┼──────────┘
                           ▼
                    DetectionSnapshot
                           │
                           ▼
                       MainViewModel
                           │
                           ▼
                       WPF View
```

## Source-of-Truth Hierarchy

### Ollama
* **Runtime Identity:** Process ancestry (descendant of `ollama.exe`), executable path (`...\Ollama\lib\...`), or direct executable match.
* **Model Metadata:** Official loopback REST endpoint `GET http://127.0.0.1:11434/api/ps`.
* **GPU Memory:** Windows WDDM Local Usage.

### LM Studio
* **Runtime Identity:** Process ancestry, executable path (`...\LM Studio\...`), or direct executable match.
* **Loaded Model:** Official CLI `lms ps --json`.
* **GPU Memory:** Windows WDDM Local Usage.

### Standalone llama.cpp
* **Runtime Identity:** Executable name (`llama-server.exe`, `llama-cli.exe`) + valid command line flags.
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
