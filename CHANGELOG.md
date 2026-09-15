# Changelog

All notable changes to **Local AI Task Manager** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-09-15

### Added

- **Windows & NVIDIA Telemetry Foundation:**
  - Real-time GPU telemetry via official NVIDIA Management Library (`nvml.dll`): device name, driver version, total/used VRAM, GPU utilization, temperature, power draw.
  - Windows memory statistics via `GlobalMemoryStatusEx`.

- **Windows WDDM Per-Process GPU Memory:**
  - Windows Display Driver Model (WDDM) memory metrics via Performance Data Helper (`\GPU Process Memory(*)`).
  - Windows WDDM per-process memory counters for `Local Usage` (GPU-local memory reported by WDDM), `Non Local Usage`, `Total Committed`, and `Shared Usage`.
  - Process CPU calculation using relative kernel and user execution time sampling.

- **AI Runtime & Model Detection:**
  - **Ollama:** Process ancestry tree matching (`ollama.exe` controller and descendant runners) and model correlation via local loopback API (`/api/ps`) with runner PID set cache invalidation.
  - **Standalone llama.cpp:** Command-line argument parsing for `llama-server.exe` and `llama-cli.exe` (`-m` model, `-c` context, and filename-based quantization inference).
  - **LM Studio (Experimental):** Process ancestry matching for runtime detection; model detection disabled pending official introspection support.
  - Strict adherence to the **UNKNOWN > WRONG** principle: speculative identification is rejected.

- **AI Workload Grouping & Composition:**
  - Groups related processes (e.g. controller + runner) into unified, first-class AI Workload cards.
  - Generates dedicated `Model`, `RuntimeService`, or `RuntimeOnly` workloads.
  - Guaranteed unique process assignment: each GPU process belongs to at most one workload.
  - Automatic segregation of non-AI processes into `OTHER GPU PROCESSES`.

- **Product UI & Inspector:**
  - High-contrast, hardware-accelerated WPF dark theme interface.
  - Dual Inspector pane: Workload metadata summary and detailed process inspection with command lines and memory breakdown.
  - Flicker-free, keyed in-place collection reconciliation preserving user selection and scroll state across sampling cycles.
  - Stable logical workload identity decoupled from runtime VRAM consumption shifts.

- **Release Packaging & Portability:**
  - Self-contained, single-file Windows x64 executable without external runtime dependencies.
  - Unsigned portable distribution running under standard user permissions (`asInvoker`).

### Known Limitations

- Supported exclusively on Windows 10/11 x64 with NVIDIA GPUs.
- AMD and Intel GPUs are not supported in v0.1.0.
- Inference performance metrics (tokens/s, time-to-first-token) and KV cache breakdowns are planned for future versions.
- LM Studio model detection remains disabled until verified introspection endpoints are available.
