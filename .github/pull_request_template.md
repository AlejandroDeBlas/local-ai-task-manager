## Description

Briefly describe the changes introduced by this pull request and the problem they solve.

## Type of Change

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] Reliability / hardening
- [ ] Documentation update
- [ ] CI / Packaging / Tooling
- [ ] New verified runtime detection (strictly adhering to UNKNOWN > WRONG)

## Checklist

- [ ] My code builds cleanly: `dotnet build LocalAITaskManager.sln -c Release`
- [ ] All unit and regression tests pass: `dotnet test LocalAITaskManager.sln -c Release`
- [ ] Code formatting verified: `dotnet format LocalAITaskManager.sln --verify-no-changes`
- [ ] Adheres to the **UNKNOWN > WRONG** principle (no speculative detection or guessing)
- [ ] No external telemetry, tracking, or unexpected network calls added
- [ ] No requirement for elevated administrator privileges
- [ ] Documentation updated if relevant
