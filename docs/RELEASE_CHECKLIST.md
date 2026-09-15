# Release Checklist

This checklist defines the steps required before creating a public release of **Local AI Task Manager**.

## Pre-Release Verification

- [ ] **Version Updated:** `Directory.Build.props` has target version (`0.1.0`), and assembly versions (`0.1.0.0`) match.
- [ ] **Clean Git Tree:** `git status` reports working tree clean.
- [ ] **Build:** Solution builds cleanly with zero warnings/errors in Release configuration:
  ```powershell
  dotnet build LocalAITaskManager.sln -c Release
  ```
- [ ] **Test Suite:** All unit and regression tests pass:
  ```powershell
  dotnet test LocalAITaskManager.sln -c Release
  ```
- [ ] **Code Formatting:** Code adheres strictly to `.editorconfig`:
  ```powershell
  dotnet format LocalAITaskManager.sln --verify-no-changes
  ```
- [ ] **Security & Privacy Audit:**
  - No external endpoints or cloud telemetry.
  - Only HTTP request is local loopback `http://127.0.0.1:11434/api/ps`.
  - Process command-lines and file paths are never persisted to disk.
  - DLL loading for `nvml.dll` strictly checks trusted system directories (`System32`, `Program Files`).
  - Application manifest enforces standard user privileges (`asInvoker`).

## Packaging & Smoke Testing

- [ ] **Release Packaging Script:** Run automated packaging pipeline:
  ```powershell
  .\scripts\release.ps1 -Version 0.1.0
  ```
- [ ] **Package Artifacts Generated:**
  - `artifacts/release/v0.1.0/LocalAITaskManager-v0.1.0-win-x64.zip`
  - `artifacts/release/v0.1.0/LocalAITaskManager-v0.1.0-win-x64.zip.sha256`
  - `artifacts/release/v0.1.0/release-manifest.json`
- [ ] **Clean Directory Smoke Test:**
  - Extract the release ZIP into a clean, isolated directory (e.g. `C:\Temp\LocalAITaskManager-v0.1.0-test`).
  - Run `LocalAITaskManager.exe` from outside the repository and verify launch without external dependencies.
- [ ] **Hardware Regressions (on system with NVIDIA GPU):**
  - Launch with zero AI workloads (displays GPU hardware overview, empty workload state, unassigned GPU processes).
  - Launch with Ollama model (`ollama run qwen2.5:1.5b`): validates model metadata, controller grouping, runner primary VRAM.
  - Unload Ollama model: verifies workload card disappears gracefully without stale selection.
  - Launch standalone `llama-server.exe`: validates CLI argument parsing, quantization inference, context length.
  - Launch concurrent workloads: verifies separate workload cards with disjoint process sets.
  - Process churn (browser/apps opening and closing): verifies no crashes or stale cards.

## Documentation & Release

- [ ] **Changelog:** `CHANGELOG.md` updated with release highlights and limitations.
- [ ] **Release Notes:** `docs/release-notes-v<version>.md` prepared.
- [ ] **README:** Verified to be user-facing, with accurate supported runtimes and real screenshot.
- [ ] **Git Tag:** Annotated tag created (e.g. `git tag -a v0.1.0 -m "Local AI Task Manager v0.1.0"`).
- [ ] **GitHub Release:** Created with ZIP package, SHA256 checksum, and manifest.
