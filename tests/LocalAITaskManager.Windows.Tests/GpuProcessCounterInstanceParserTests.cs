using LocalAITaskManager.Windows.PerformanceCounters;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class GpuProcessCounterInstanceParserTests
{
    [Fact]
    public void TryParse_StandardFormat_ExtractsAllFields()
    {
        string instance = "pid_18420_luid_0x00000000_0x0000ABCD_phys_0";

        bool success = GpuProcessCounterInstanceParser.TryParse(instance, out ParsedInstanceInfo? info);

        Assert.True(success);
        Assert.NotNull(info);
        Assert.Equal(18420, info.Pid);
        Assert.Equal(0, info.LuidHigh);
        Assert.Equal(0xABCD, info.LuidLow);
        Assert.Equal(0xABCD, info.Luid);
        Assert.Equal(0, info.PhysicalAdapterIndex);
    }

    [Fact]
    public void TryParse_WithDuplicateCounterIndexSuffix_ExtractsCorrectPid()
    {
        string instance = "pid_39792_luid_0x00000000_0x000182D0_phys_0#1";

        bool success = GpuProcessCounterInstanceParser.TryParse(instance, out ParsedInstanceInfo? info);

        Assert.True(success);
        Assert.NotNull(info);
        Assert.Equal(39792, info.Pid);
        Assert.Equal(0, info.PhysicalAdapterIndex);
    }

    [Fact]
    public void TryParse_PidOnly_ExtractsPid()
    {
        string instance = "pid_1234";

        bool success = GpuProcessCounterInstanceParser.TryParse(instance, out ParsedInstanceInfo? info);

        Assert.True(success);
        Assert.NotNull(info);
        Assert.Equal(1234, info.Pid);
        Assert.Null(info.Luid);
        Assert.Null(info.PhysicalAdapterIndex);
    }

    [Theory]
    [InlineData("whatever")]
    [InlineData("pid_")]
    [InlineData("pid_abc")]
    [InlineData("gpu_process_memory")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_MalformedOrNullString_ReturnsFalseWithoutException(string? invalidInstance)
    {
        bool success = GpuProcessCounterInstanceParser.TryParse(invalidInstance, out ParsedInstanceInfo? info);

        Assert.False(success);
        Assert.Null(info);
    }
}
