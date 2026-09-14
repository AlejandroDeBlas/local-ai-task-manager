namespace LocalAITaskManager.Core.Abstractions;

public interface ICommandLineParser
{
    IReadOnlyList<string> ParseArguments(string commandLine);
}
