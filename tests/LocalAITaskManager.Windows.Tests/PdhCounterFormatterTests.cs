using LocalAITaskManager.Windows.PerformanceCounters;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class PdhCounterFormatterTests
{
    [Fact]
    public void ExtractItemValue_ValidStatus_ReturnsValue()
    {
        var validItem = new PdhFmtCounterValue
        {
            CStatus = PdhNative.PDH_CSTATUS_VALID_DATA,
            largeValue = 1048576L
        };

        ulong? val = PdhCounterFormatter.ExtractItemValue(validItem);

        Assert.NotNull(val);
        Assert.Equal(1048576UL, val.Value);
    }

    [Fact]
    public void ExtractItemValue_NewDataStatus_ReturnsValue()
    {
        var newItem = new PdhFmtCounterValue
        {
            CStatus = PdhNative.PDH_CSTATUS_NEW_DATA,
            largeValue = 2097152L
        };

        ulong? val = PdhCounterFormatter.ExtractItemValue(newItem);

        Assert.NotNull(val);
        Assert.Equal(2097152UL, val.Value);
    }

    [Fact]
    public void ExtractItemValue_RealZeroWithValidStatus_PreservesZero()
    {
        var zeroItem = new PdhFmtCounterValue
        {
            CStatus = PdhNative.PDH_CSTATUS_VALID_DATA,
            largeValue = 0L
        };

        ulong? val = PdhCounterFormatter.ExtractItemValue(zeroItem);

        Assert.NotNull(val);
        Assert.Equal(0UL, val.Value);
    }

    [Theory]
    [InlineData(PdhNative.PDH_CSTATUS_NO_INSTANCE)]
    [InlineData(PdhNative.PDH_NO_DATA)]
    [InlineData(0xC0000BB8)]
    [InlineData(0xFFFFFFFF)]
    public void ExtractItemValue_InvalidStatus_ReturnsNullNotZero(uint invalidStatus)
    {
        var invalidItem = new PdhFmtCounterValue
        {
            CStatus = invalidStatus,
            largeValue = 1048576L
        };

        ulong? val = PdhCounterFormatter.ExtractItemValue(invalidItem);

        Assert.Null(val);
    }

    [Fact]
    public void ExtractItemValue_NegativeLargeValue_ReturnsNull()
    {
        var negativeItem = new PdhFmtCounterValue
        {
            CStatus = PdhNative.PDH_CSTATUS_VALID_DATA,
            largeValue = -1L
        };

        ulong? val = PdhCounterFormatter.ExtractItemValue(negativeItem);

        Assert.Null(val);
    }
}
