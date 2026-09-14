using LocalAITaskManager.Core.Services;
using Xunit;

namespace LocalAITaskManager.Core.Tests;

public class ByteFormatterTests
{
    [Fact]
    public void Format_NullValue_ReturnsUnavailableFallback()
    {
        string result = ByteFormatter.Format(null);
        Assert.Equal("Unavailable", result);

        string customFallback = ByteFormatter.Format(null, "—");
        Assert.Equal("—", customFallback);
    }

    [Fact]
    public void Format_ZeroBytes_ReturnsZeroB()
    {
        string result = ByteFormatter.Format(0);
        Assert.Equal("0 B", result);
    }

    [Fact]
    public void Format_Kilobytes_FormatsCorrectly()
    {
        string result = ByteFormatter.Format(1024);
        Assert.Equal("1 KB", result);

        string result2 = ByteFormatter.Format(2048);
        Assert.Equal("2 KB", result2);
    }

    [Fact]
    public void Format_Megabytes_FormatsCorrectly()
    {
        ulong bytes = 140UL * 1024UL * 1024UL;
        string result = ByteFormatter.Format(bytes);
        Assert.Equal("140 MB", result);
    }

    [Fact]
    public void Format_Gigabytes_FormatsCorrectly()
    {
        ulong bytes = (ulong)(14.2 * 1024 * 1024 * 1024);
        string result = ByteFormatter.Format(bytes);
        Assert.Equal("14.2 GB", result);
    }

    [Fact]
    public void FormatRatio_CalculatesCorrectGbRatio()
    {
        ulong used = (ulong)(14.2 * 1024 * 1024 * 1024);
        ulong total = 16UL * 1024UL * 1024UL * 1024UL;
        string result = ByteFormatter.FormatRatio(used, total);
        Assert.Equal("14.2 / 16.0 GB", result);
    }
}
