# Local AI Task Manager v0.1.0

First public release of **Local AI Task Manager** — a zero-configuration Windows desktop utility to monitor local AI workloads and GPU memory consumption.

## Highlights

- **Workload-Centric Monitoring:** Displays AI models and runtimes as primary cards rather than raw, disjointed process IDs.
- **Windows WDDM Per-Process GPU Memory:** Inspects Windows Display Driver Model (WDDM) local video memory counters per process, using Local Usage as the primary metric.
- **Zero Configuration:** Automatically discovers running instances of supported runtimes without daemons, agents, or CLI wrappers.
- **Dual Inspector:** Deep-dive into workload configuration, quantization, context size, and individual member processes.
- **Completely Private:** No external network communication, zero telemetry, zero analytics, and no cloud backends. Only local loopback querying to Ollama on 127.0.0.1.

## Supported Runtimes

| Runtime | Runtime Detection | Model Detection | Status |
| :--- | :---: | :---: | :--- |
| **Ollama** | Yes | Yes | Supported |
| **standalone llama.cpp** | Yes | Yes | Supported |
| **LM Studio** | Experimental | No | Experimental |

## How to Run

1. Download `LocalAITaskManager-v0.1.0-win-x64.zip` from the Assets below.
2. Extract the archive to any directory.
3. Run `LocalAITaskManager.exe`.
4. No installation, accounts, or administrator privileges required.

> [!NOTE]
> **Windows SmartScreen:** Windows SmartScreen may display an informational notice because this unsigned, open-source binary is newly released. You can verify the checksum below or build directly from source.

## Privacy & Security

- **No Telemetry:** No analytics, tracking, or network telemetry.
- **No Cloud Backend:** Operates entirely offline on your machine.
- **Local Network Only:** The only network request made is a loopback query to the local Ollama API on `http://127.0.0.1:11434/api/ps`.
- **Standard Privileges:** Runs as a standard user (`asInvoker`) without requiring administrator elevation.

## Known Limitations

- Supported only on **Windows 10/11 x64** with **NVIDIA GPUs** (NVML driver required).
- AMD and Intel GPUs are not supported in v0.1.0.
- Inference performance counters (tokens/second, time-to-first-token) and KV cache memory breakdowns are planned for upcoming releases.
- LM Studio model introspection is disabled pending official daemon-less support.

## SHA256 Verification
 
To verify the integrity of your download:

```powershell
Get-FileHash -Algorithm SHA256 .\LocalAITaskManager-v0.1.0-win-x64.zip
```

Expected checksum:
```text
298638566f5bfd59ceb10b07d8fc5c613a3304dd8b20c8e1fdead66c3de382d4
```

You can also verify against the published asset checksum file `LocalAITaskManager-v0.1.0-win-x64.zip.sha256`.
