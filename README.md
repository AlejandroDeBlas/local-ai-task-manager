# Local AI Task Manager

Task Manager for Local AI.

Local AI Task Manager is an experimental, zero-configuration Windows desktop utility designed to monitor local AI workloads and GPU memory consumption without relying on external servers, CLI wrappers, or `nvidia-smi`.

> [!NOTE]
> This repository represents **Phase 1: Windows/NVIDIA telemetry foundation + minimal GUI**.

<!-- Screenshot placeholder -->
```text
┌──────────────────────────────────────────────────────────┐
│ Local AI Task Manager                                   │
│                                                          │
│ NVIDIA GeForce RTX 4070 Ti SUPER                        │
│ [████████████████████████░░░]  14.2 / 16.0 GB           │
│                                                          │
│ GPU 92%     TEMP 66°C     POWER 224W                    │
│ RAM 18.1 / 31.8 GB                                      │
│                                                          │
│ GPU PROCESSES                                            │
│ ──────────────────────────────────────────────────────── │
│ Process              PID      VRAM       RAM       CPU   │
│ LM Studio.exe       18420    12.8 GB     1.2 GB     4%   │
│ dwm.exe              1880     0.3 GB    140 MB      1%   │
│ firefox.exe         10240     0.2 GB    800 MB      2%   │
└──────────────────────────────────────────────────────────┘
```

## Features & Telemetry Sources

| Metric | Source |
| :--- | :--- |
| GPU name | NVML (`nvmlDeviceGetName`) |
| Total/used VRAM | NVML (`nvmlDeviceGetMemoryInfo`) |
| GPU utilization | NVML (`nvmlDeviceGetUtilizationRates`) |
| Temperature | NVML (`nvmlDeviceGetTemperature`) |
| Power | NVML (`nvmlDeviceGetPowerUsage`) |
| Process GPU memory | Windows PDH (`\GPU Process Memory(*)\Dedicated Usage` & `Shared Usage`) |
| System RAM | Win32 (`GlobalMemoryStatusEx`) |
| Process RAM | Win32 / .NET (`Process.WorkingSet64`) |
| Process CPU | Calculated from `Process.TotalProcessorTime` deltas |
| Command line | WMI (`Win32_Process.CommandLine`) with PID caching |

## Known Limitations (Phase 1)

* **Phase 1 does not identify AI runtimes or models yet.**
* **Process GPU memory on Windows/WDDM comes from Windows GPU Process Memory counters.** On standard GeForce drivers under WDDM, `nvmlProcessInfo_t.usedGpuMemory` is unsupported (`NVML_VALUE_NOT_AVAILABLE`) by NVIDIA because memory is managed by the Windows kernel mode driver (KMD).
* **Per-process memory is aggregated across adapters in Phase 1.** Multi-GPU systems aggregate process dedicated and shared memory across adapters until DXGI/LUID adapter mapping is added in subsequent phases.
* **Weights/KV cache/runtime memory cannot yet be separated.** Displayed VRAM represents the total dedicated GPU memory allocated to the process according to Windows.

## Privacy & Security

* **Zero telemetry**: No data collection, analytics, or outbound internet traffic.
* **No elevation**: Runs under standard user privileges without UAC prompts.
* **No subprocesses**: No background execution of `nvidia-smi`, PowerShell, or shell commands during runtime.
* **Safe native loading**: Only loads official NVIDIA driver libraries from trusted system directories (`%SystemRoot%\System32\nvml.dll` or `%ProgramW6432%\NVIDIA Corporation\NVSMI\nvml.dll`). Never loads from working directories or arbitrary PATH.

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

The self-contained executable will be generated at:
`artifacts/publish/win-x64/LocalAITaskManager.exe`

## Architecture

See [docs/architecture.md](docs/architecture.md) for details on project layering, sampling pipeline, and threading models.

## License

This project is licensed under the [MIT License](LICENSE).
