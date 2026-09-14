# Contributing to Local AI Task Manager

Thank you for your interest in contributing. Please review the following guidelines.

## Requirements

* Windows 10 or Windows 11 (x64)
* .NET 10.0 SDK or later
* Git

## Build

Clone the repository and build using the .NET CLI:

```powershell
dotnet restore
dotnet build LocalAITaskManager.sln -c Release --no-restore
```

## Tests

Execute unit tests:

```powershell
dotnet test LocalAITaskManager.sln -c Release --no-build
```

## Code Formatting

All code must adhere to `.editorconfig` and pass formatting checks without differences:

```powershell
dotnet format LocalAITaskManager.sln --verify-no-changes
```

## Pull Request Expectations

* Target `main`.
* Ensure builds, tests, and formatting checks pass.
* Keep PRs scoped to single, coherent improvements.
* Avoid adding runtime dependencies, subprocess invocations (e.g. `nvidia-smi`), or elevated permission requirements.
