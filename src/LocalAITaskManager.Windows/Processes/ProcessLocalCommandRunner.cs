using System.Diagnostics;
using LocalAITaskManager.Core.Abstractions;

namespace LocalAITaskManager.Windows.Processes;

public sealed class ProcessLocalCommandRunner : ILocalCommandRunner
{
    public async Task<CommandExecutionResult> ExecuteAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return new CommandExecutionResult(-1, string.Empty, "Executable not found", false);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            if (!process.Start())
            {
                return new CommandExecutionResult(-1, string.Empty, "Failed to start process", false);
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            string stdout = await stdoutTask.ConfigureAwait(false);
            string stderr = await stderrTask.ConfigureAwait(false);

            return new CommandExecutionResult(process.ExitCode, stdout, stderr, false);
        }
        catch (OperationCanceledException)
        {
            bool timedOut = !cancellationToken.IsCancellationRequested;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best-effort process cleanup
            }

            if (timedOut)
            {
                return new CommandExecutionResult(-1, string.Empty, "Process execution timed out", true);
            }

            throw;
        }
        catch (Exception ex)
        {
            return new CommandExecutionResult(-1, string.Empty, ex.Message, false);
        }
    }
}
