namespace LocalAITaskManager.Core.Abstractions;

public sealed record CommandExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut
);

public interface ILocalCommandRunner
{
    Task<CommandExecutionResult> ExecuteAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
