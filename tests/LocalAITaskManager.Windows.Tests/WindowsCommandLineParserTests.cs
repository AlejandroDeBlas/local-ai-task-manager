using LocalAITaskManager.Windows.Processes;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class WindowsCommandLineParserTests
{
    private readonly WindowsCommandLineParser _parser = new();

    [Fact]
    public void ParseArguments_StandardFlags_ParsesCorrectly()
    {
        string cmd = @"llama-server.exe -m C:\Models\a.gguf -c 32768";
        var args = _parser.ParseArguments(cmd);

        Assert.Equal(5, args.Count);
        Assert.Equal("llama-server.exe", args[0]);
        Assert.Equal("-m", args[1]);
        Assert.Equal(@"C:\Models\a.gguf", args[2]);
        Assert.Equal("-c", args[3]);
        Assert.Equal("32768", args[4]);
    }

    [Fact]
    public void ParseArguments_QuotedPathsWithSpaces_PreservesQuotesContent()
    {
        string cmd = @"llama-server.exe -m ""C:\AI Models\a.gguf"" --ctx-size 8192";
        var args = _parser.ParseArguments(cmd);

        Assert.Equal(5, args.Count);
        Assert.Equal("llama-server.exe", args[0]);
        Assert.Equal("-m", args[1]);
        Assert.Equal(@"C:\AI Models\a.gguf", args[2]);
        Assert.Equal("--ctx-size", args[3]);
        Assert.Equal("8192", args[4]);
    }

    [Fact]
    public void ParseArguments_EqualsSyntax_ParsesCombinedArg()
    {
        string cmd = @"llama-server.exe --model=""C:\AI Models\a.gguf"" --ctx-size=16384";
        var args = _parser.ParseArguments(cmd);

        Assert.Equal(3, args.Count);
        Assert.Equal("llama-server.exe", args[0]);
        Assert.Equal(@"--model=C:\AI Models\a.gguf", args[1]);
        Assert.Equal("--ctx-size=16384", args[2]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseArguments_NullOrWhitespace_ReturnsEmptyList(string? cmd)
    {
        var args = _parser.ParseArguments(cmd!);
        Assert.Empty(args);
    }
}
