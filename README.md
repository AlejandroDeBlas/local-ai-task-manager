# Local AI Task Manager

Task Manager for Local AI on Windows.

See which local AI models are using your GPU, which runtime they belong to, and how much GPU-local memory their primary process is consuming — without relying on external servers, background daemons, or `nvidia-smi`.

![Local AI Task Manager v0.1](docs/assets/local-ai-task-manager-v0.1.png)

---

## Features

* **Workload-Centric Architecture:** Group disjointed processes (e.g. controller + runner) into clean, first-class AI Workload cards.
* **WDDM-Accurate GPU Accounting:** Queries real Windows Display Driver Model (WDDM) local video memory allocations per process.
* **Zero Configuration:** Automatically identifies running models and runtimes without setup or manual tagging.
* **Comprehensive Telemetry:** Real-time NVIDIA GPU metrics (VRAM, utilization, temperature, power) and system memory usage.
* **Deep Inspector:** Inspect model configuration (quantization, context length, parameters), workload resource breakdown, and underlying command lines.
* **Flicker-Free Reconciliation:** Keyed in-place collection reconciliation preserves user selection and scroll state across telemetry cycles.
* **Unassigned Process Segregation:** Cleanly separates AI workloads from unrelated system and desktop GPU processes (`dwm.exe`, browsers, IDEs).
* **100% Offline & Private:** Zero analytics, zero cloud backends, zero background trackers.

---

## Supported Runtimes

| Runtime | Runtime Detection | Model Detection | Status |
| :--- | :---: | :---: | :--- |
| **Ollama** | Yes | Yes | Supported |
| **standalone llama.cpp** | Yes | Yes | Supported |
| **LM Studio** | Experimental | No | Experimental |

> [!NOTE]
> **UNKNOWN > WRONG Principle:** Local AI Task Manager strictly adheres to the principle of never guessing. If a workload or model cannot be unambiguously attributed with verifiable evidence, it remains classified as `Unknown` rather than displaying speculative information.

---

## Download

Get the latest release for **Windows 10/11 (x64)**:

* [Download Local AI Task Manager v0.1.0](https://github.com/AlejandroDeBlas/local-ai-task-manager/releases)

Distributed as an unsigned, portable single-file executable inside a standalone ZIP archive (`LocalAITaskManager-v0.1.0-win-x64.zip`). No installer, registry changes, or administrator permissions required.

---

## Quick Start

1. Download `LocalAITaskManager-v0.1.0-win-x64.zip` from GitHub Releases.
2. Extract the archive to any folder.
3. Run `LocalAITaskManager.exe`.
4. Launch your local AI models using your preferred runtime (e.g. `ollama run qwen2.5:1.5b` or `llama-server.exe -m model.gguf`).

> [!TIP]
> **Windows SmartScreen:** Windows SmartScreen may display an informational notice because this unsigned, open-source binary is newly published. You can verify the published SHA256 checksum or build directly from source.

---

## How It Works

Local AI Task Manager operates across three cleanly separated architectural layers:

```text
LocalAITaskManager.App (WPF GUI, ViewModels, Dispatcher orchestration)
         │
         ▼
LocalAITaskManager.Windows (P/Invoke, NVML, PDH, Win32 RAM, Process ancestry, Workload Detectors)
         │
         ▼
LocalAITaskManager.Core (Domain entities, abstractions, formatters, pure logic)
```

1. **Hardware Telemetry:** Direct P/Invoke bindings to NVIDIA Management Library (`nvml.dll`) query device health, temperature, power, and overall adapter memory.
2. **Process GPU Memory:** Windows Performance Data Helper (`pdh.dll`) samples kernel-managed `\GPU Process Memory(*)` counters to measure true resident local memory.
3. **Runtime & Model Correlation:**
   - **Ollama:** Detects process ancestry descending from `ollama.exe` and interrogates the official loopback endpoint (`GET http://127.0.0.1:11434/api/ps`). Cached responses are instantly invalidated whenever the runner PID set changes.
   - **standalone llama.cpp:** Inspects native command-line tokens via `CommandLineToArgvW` to capture `-m`/`--model`, `-c`/`--ctx-size`, and infers GGUF quantization patterns.
   - **LM Studio:** Detects runtime process ancestry. Model correlation is deliberately disabled until verified, daemon-less introspection APIs become available.

---

## Memory Semantics

Under standard Windows desktop operation, NVIDIA GPUs function under the **Windows Display Driver Model (WDDM)**. Under WDDM, the Windows Kernel Mode Driver (`dxgkrnl.sys`) is the authoritative arbiter of video memory allocations, and NVML per-process metrics (`nvmlProcessInfo_t.usedGpuMemory`) return `NVML_VALUE_NOT_AVAILABLE`.

To report accurate process memory, Local AI Task Manager tracks:
* **Primary VRAM (WDDM Local Usage):** Memory currently resident on the dedicated physical video memory of the GPU adapter for the primary workload process.
* **Non-Local Usage:** Video memory allocated on behalf of the process residing in system RAM.
* **Total Committed:** Total virtual video memory committed by the Windows video memory manager.

---

## Privacy & Security

* **Zero Telemetry:** No analytics, tracking, or network telemetry of any kind.
* **No Cloud Backend:** The application does not contact external servers or remote endpoints.
* **Localhost Loopback Only:** The only network socket opened is a local HTTP query to `127.0.0.1:11434/api/ps` for Ollama introspection.
* **In-Memory Operation:** Process command lines and executable paths are inspected transiently in memory and never written to disk or logged.
* **Trusted Library Loading:** `nvml.dll` is strictly resolved from trusted system directories (`System32` and `Program Files\NVIDIA Corporation\NVSMI`).
* **Standard User Privileges:** Runs as a standard user process (`asInvoker`) without requiring administrative elevation.

---

## Known Limitations

* Supported exclusively on **Windows 10 / 11 (x64)** with **NVIDIA GPUs** (standard drivers installed).
* AMD (ROCm/DirectML) and Intel (oneAPI) GPUs are not supported in v0.1.0.
* Per-process multi-GPU adapter mapping via DXGI LUID is planned for future releases.
* LM Studio model introspection is disabled pending official support.
* Live inference speed (tokens/s, time-to-first-token) and KV cache breakdowns are not yet implemented.

---

## Build from Source

### Prerequisites

* Windows 10 / 11 (x64)
* [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
* Git

### Build & Test

```powershell
# Clone the repository
git clone https://github.com/AlejandroDeBlas/local-ai-task-manager.git
cd local-ai-task-manager

# Restore and compile
dotnet restore
dotnet build LocalAITaskManager.sln -c Release --no-restore

# Run automated test suite
dotnet test LocalAITaskManager.sln -c Release --no-build

# Verify code style and formatting
dotnet format LocalAITaskManager.sln --verify-no-changes
```

### Packaging

To build the self-contained single-file executable and release package:

```powershell
.\scripts\release.ps1 -Version 0.1.0
```

Outputs are staged in `artifacts/release/v0.1.0/`.

---

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on code style, testing, and our **UNKNOWN > WRONG** verification policy.

---

## Roadmap

Planned for upcoming releases:

- [ ] **v0.2.0:**
  - Token generation speed (decode tokens/s, prefill tokens/s, TTFT)
  - Memory breakdown (Model weights vs. KV cache vs. runtime overhead)
  - AMD Radeon support (DirectML / WDDM)
  - Multi-GPU per-device process allocation mapping
  - ComfyUI & vLLM runtime detection
  - Optional system tray icon and minimize-to-tray

---

## License

Distributed under the **MIT License**. See [LICENSE](LICENSE) for full text.
