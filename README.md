# Local AI Task Manager

Task Manager for Local AI.

Local AI Task Manager is an experimental, zero-configuration Windows desktop utility designed to monitor local AI workloads and GPU memory consumption without relying on external servers, CLI wrappers, or `nvidia-smi`.

> [!NOTE]
> This repository represents **Phase 2: Runtime and Model Identification**.

<!-- Screenshot placeholder -->
```text
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ Local AI Task Manager                                                                  │
│                                                                                        │
│ NVIDIA GeForce RTX 4070 Ti SUPER                                                       │
│ [████████████████████████░░░]  14.2 / 16.0 GB                                          │
│                                                                                        │
│ GPU 92%     TEMP 66°C     POWER 224W                                                   │
│ RAM 18.1 / 31.8 GB                                                                     │
│                                                                                        │
│ GPU PROCESSES                                                                          │
│ ────────────────────────────────────────────────────────────────────────────────────── │
│ Process             Runtime       Model                         PID      VRAM   RAM    │
│ llama-server.exe    Ollama        qwen2.5:1.5b                 46588    1.3 GB 1.0 GB  │
│ llama-server.exe    llama.cpp     Ornith-1.5-35B-heretic-Q4_K  25140    5.0 GB 5.7 GB  │
│ LM Studio child     LM Studio     mistral-small-24b            32110   13.2 GB 1.4 GB  │
│ dwm.exe             —             —                             1276   389 MB  150 MB  │
│ firefox.exe         —             —                            12000   180 MB  900 MB  │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

## Feature Status

### Supported

**Hardware:**
* NVIDIA / Windows (NVML + WDDM PDH)

**Runtime Detection:**
* standalone `llama.cpp` / `llama-server`
* `Ollama`
* `LM Studio`

**Model Detection:**
* `llama.cpp`: direct `-m` / `--model`, `-c` / `--ctx-size`, GGUF filename quantization inference
* `Ollama`: official local loopback `/api/ps` endpoint (model, quantization, parameters, context, VRAM size)
* `LM Studio`: official `lms ps --json` CLI integration

### Not Yet (Future Phases)
* ComfyUI
* Generic Python/CUDA classification
* Tokens/s (prefill, decode, TTFT)
* KV cache memory breakdown
* Weights vs runtime memory breakdown
* AMD / Intel

---

## Principle: UNKNOWN > WRONG

Local AI Task Manager strictly adheres to the principle of never guessing. If an AI workload cannot be unambiguously attributed to a specific model or runtime with verifiable evidence, it remains classified as `—` (unknown) rather than displaying speculative information.

---

## Source-of-Truth Hierarchy

| Target | Runtime Identity | Model Metadata | GPU Memory |
| :--- | :--- | :--- | :--- |
| **Ollama** | Process ancestry (`ollama.exe`), executable path (`...\Ollama\lib\...`) | Official loopback API `GET /api/ps` | Windows WDDM Local Usage |
| **LM Studio** | Process ancestry, executable path (`...\LM Studio\...`) | Official CLI `lms ps --json` | Windows WDDM Local Usage |
| **standalone llama.cpp** | Executable name (`llama-server.exe`, `llama-cli.exe`), command line flags | `-m` / `--model` arguments, `-c` context, filename quantization inference | Windows WDDM Local Usage |

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

The process-table `VRAM` column uses WDDM **Local Usage** (`\GPU Process Memory(*)\Local Usage`).

Per-process Local Usage values should not be expected to sum exactly to NVML's global Used VRAM because the two APIs expose different accounting scopes and WDDM may involve shared allocations, driver allocations, and timing differences between sampling intervals.

Under Windows WDDM, `Local Usage` represents memory physically resident on the discrete GPU adapter, while `Non Local Usage` represents memory paged out or located in system memory. `Dedicated Usage`, `Shared Usage`, and `Total Committed` are tracked separately and available in the process inspector panel.

## Privacy & Security

* **Zero external telemetry**: No data collection, analytics, or outbound internet traffic.
* **Loopback-only probing**: The Ollama client strictly communicates with `http://127.0.0.1:11434/api/ps` with short timeouts (500ms) and no proxies.
* **Controlled subprocess execution**: LM Studio model queries invoke `lms ps --json` directly without invoking command shells (`cmd.exe`, `powershell.exe`) or PATH fallbacks.
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
