# Local AI Task Manager

Task Manager for Local AI.

Local AI Task Manager is an experimental, zero-configuration Windows desktop utility designed to monitor local AI workloads and GPU memory consumption without relying on external servers, CLI wrappers, or `nvidia-smi`.

> [!NOTE]
> This repository represents **Phase 3: AI Workload Composition + Product UI**.

<!-- ASCII UI Mockup -->
```text
┌─────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Local AI Task Manager                                                                           │
│ Task Manager for Local AI                                                                       │
│                                                                                                 │
│ NVIDIA GeForce RTX 3080 Ti • Driver 552.22  │ VRAM [██████████████░░░] 12.8 / 16.0 GB           │
│ UTIL 42%   TEMP 58°C   POWER 185W   RAM 18.2 / 31.8 GB                                          │
├────────────────────────────────────────────────────────┬────────────────────────────────────────┤
│ AI WORKLOADS (2 active)                                │ INSPECTOR                              │
│ ┌────────────────────────────────────────────────────┐ │ Qwen2.5 14B Q4_K_M                     │
│ │ Qwen2.5 14B Q4_K_M                        10.8 GB  │ │ Runtime: Ollama (Confidence: Confirmed)│
│ │ [Ollama] [Q4_K_M] [Context 32K]                    │ │                                        │
│ │ 2 processes • PID 46588      RAM: 1.2 GB  CPU: 0.4%│ │ MODEL CONFIGURATION                    │
│ └────────────────────────────────────────────────────┘ │ Quantization: Q4_K_M                   │
│ ┌────────────────────────────────────────────────────┐ │ Context: 32,768 tokens                 │
│ │ LM Studio                                   1.4 GB │ │                                        │
│ │ [LM Studio • Experimental] [Model detection unavail]│ WORKLOAD RESOURCES                     │
│ │ 1 process • PID 32110        RAM: 620 MB  CPU: 0.1%│ │ Primary VRAM: 10.8 GB (WDDM Local)     │
│ └────────────────────────────────────────────────────┘ │ Working Set: 1.2 GB                    │
│                                                        │                                        │
│ OTHER GPU PROCESSES (3 processes)                      │ Workload Processes:                    │
│ Process              PID       VRAM        RAM    CPU  │ • ollama_llama_server.exe (PID 46588)  │
│ dwm.exe             1276     389 MB     150 MB   1.2%  │ • ollama.exe (PID 1240)                │
│ chrome.exe         14200     220 MB     840 MB   0.5%  │                                        │
└────────────────────────────────────────────────────────┴────────────────────────────────────────┘
```

## Feature Status

### Supported

**Hardware:**
* NVIDIA / Windows (NVML + WDDM PDH)

**Workload Composition & Product UI:**
* AI Workloads as primary top-level domain entities
* Separation of AI workloads from unassigned `OTHER GPU PROCESSES`
* Dual Inspector pane: Workload metadata + inspected process command-line / WDDM breakdown
* Smooth, flicker-free keyed collection reconciliation preserving user selections

**Runtime Detection:**
* standalone `llama.cpp` / `llama-server`
* `Ollama`
* `LM Studio` (experimental runtime / model detection not validated)

**Model Detection:**
* `llama.cpp`: direct `-m` / `--model`, `-c` / `--ctx-size`, GGUF filename quantization inference (`High` confidence with structural flags, `Medium` for executable-only)
* `Ollama`: official local loopback `/api/ps` endpoint (model, quantization, parameters, context, VRAM size; cache invalidated instantly on runner PID set changes)
* `LM Studio`: model detection not validated / disabled (process identified, model left unassigned per UNKNOWN > WRONG)

### Not Yet (Future Phases)
* ComfyUI
* Generic Python/CUDA classification
* Tokens/s (prefill, decode, TTFT)
* KV cache memory breakdown
* Weights vs runtime memory breakdown
* AMD / Intel

---

## Principle: UNKNOWN > WRONG

Local AI Task Manager strictly adheres to the principle of never guessing. If an AI workload cannot be unambiguously attributed to a specific model or runtime with verifiable evidence, it remains classified as `Unknown` rather than displaying speculative information.

---

## Source-of-Truth Hierarchy

| Target | Runtime Identity | Model Metadata | GPU Memory |
| :--- | :--- | :--- | :--- |
| **Ollama** | Process ancestry (`ollama.exe`), executable path (`...\Ollama\lib\...`) | Official loopback API `GET /api/ps` (runner PID set invalidated cache) | Windows WDDM Local Usage |
| **LM Studio** | Process ancestry, executable path (`...\LM Studio\...`) | *Not validated / disabled* (model unassigned) | Windows WDDM Local Usage |
| **standalone llama.cpp** | Executable name (`llama-server.exe`, `llama-cli.exe`) + structural flags | `-m` / `--model` arguments, `-c` context, filename quantization inference | Windows WDDM Local Usage |

---

## Features & Telemetry Sources

| Metric | Source |
| :--- | :--- |
| GPU name | NVML (`nvmlDeviceGetName`) |
| Global total/used VRAM | NVML (`nvmlDeviceGetMemoryInfo`) |
| GPU utilization | NVML (`nvmlDeviceGetUtilizationRates`) |
| Temperature | NVML (`nvmlDeviceGetTemperature`) |
| Power | NVML (`nvmlDeviceGetPowerUsage`) |
| Process local GPU memory | Windows PDH (`\GPU Process Memory(*)\Local Usage`) |
| Process non-local GPU memory | Windows PDH (`\GPU Process Memory(*)\Non Local Usage`) |
| Process total committed | Windows PDH (`\GPU Process Memory(*)\Total Committed`) |
| Process dedicated usage | Windows PDH (`\GPU Process Memory(*)\Dedicated Usage`) |
| Process shared usage | Windows PDH (`\GPU Process Memory(*)\Shared Usage`) |
| System RAM | Win32 (`GlobalMemoryStatusEx`) |
| Process RAM | Win32 / .NET (`Process.WorkingSet64`) |
| Process CPU | Calculated from `Process.TotalProcessorTime` deltas |
| Command line | WMI (`Win32_Process.CommandLine`) with PID caching |

## WDDM Memory Semantics & Primary "VRAM" Metric

The workload cards and other GPU process rows use WDDM **Local Usage** (`\GPU Process Memory(*)\Local Usage`) as their primary VRAM metric. For multi-process AI workloads, the workload card displays the primary model process's local memory usage (`PrimaryLocalGpuMemoryBytes`) rather than summing member allocations (since WDDM process-level allocations are not strictly additive and can double-count shared mappings).

Per-process Local Usage values should not be expected to sum exactly to NVML's global Used VRAM because the two APIs expose different accounting scopes and WDDM may involve shared allocations, driver allocations, and timing differences between sampling intervals.

Under Windows WDDM, `Local Usage` represents memory physically resident on the discrete GPU adapter, while `Non Local Usage` represents memory paged out or located in system memory. `Dedicated Usage`, `Shared Usage`, and `Total Committed` are tracked separately and available in the process inspector panel.

## Privacy & Security

* **Zero external telemetry**: No data collection, analytics, or outbound internet traffic.
* **Loopback-only probing**: The Ollama client strictly communicates with `http://127.0.0.1:11434/api/ps` with short timeouts (500ms) and no proxies.
* **Controlled subprocess execution**: Subprocess execution for unvalidated tools is deactivated to prevent launching unauthorized background daemons or triggering unexpected side-effects.
* **No command line exposure**: Arguments and sensitive file paths are never written to disk, sent across networks, or exposed beyond the local inspector.

## Building & Running

### Prerequisites

* Windows 10/11 x64
* .NET 10.0 SDK
* NVIDIA GPU with driver installed

### Build

```powershell
dotnet restore
dotnet build LocalAITaskManager.sln -c Release --no-restore
```

### Run

```powershell
dotnet run --project src/LocalAITaskManager.App -c Release
```

### Run Tests

```powershell
dotnet test LocalAITaskManager.sln -c Release --no-build
```

### Publish Single-File Executable

```powershell
.\scripts\publish.ps1
```
