namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Static or slowly changing metadata associated with a process.
/// </summary>
public sealed record ProcessMetadata(
    int Pid,
    string ProcessName,
    string? ExecutablePath,
    string? CommandLine
);
