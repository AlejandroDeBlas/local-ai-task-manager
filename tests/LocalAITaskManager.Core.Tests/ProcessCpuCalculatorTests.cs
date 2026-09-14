using LocalAITaskManager.Core.Services;
using Xunit;

namespace LocalAITaskManager.Core.Tests;

public class ProcessCpuCalculatorTests
{
    [Fact]
    public void Calculate_NormalDelta_ReturnsCorrectPercentage()
    {
        // 1 second wall time, 4 cores, process used 0.5s of CPU time
        // Expected CPU: 0.5s / (1.0s * 4) * 100 = 12.5%
        TimeSpan deltaCpu = TimeSpan.FromMilliseconds(500);
        TimeSpan deltaWall = TimeSpan.FromSeconds(1);
        int cores = 4;

        double? cpu = ProcessCpuCalculator.Calculate(deltaCpu, deltaWall, cores);

        Assert.NotNull(cpu);
        Assert.Equal(12.5, cpu.Value, 2);
    }

    [Fact]
    public void Calculate_ZeroWallTime_ReturnsNull()
    {
        TimeSpan deltaCpu = TimeSpan.FromMilliseconds(100);
        TimeSpan deltaWall = TimeSpan.Zero;
        int cores = 8;

        double? cpu = ProcessCpuCalculator.Calculate(deltaCpu, deltaWall, cores);

        Assert.Null(cpu);
    }

    [Fact]
    public void Calculate_NegativeWallTime_ReturnsNull()
    {
        TimeSpan deltaCpu = TimeSpan.FromMilliseconds(100);
        TimeSpan deltaWall = TimeSpan.FromSeconds(-1);
        int cores = 8;

        double? cpu = ProcessCpuCalculator.Calculate(deltaCpu, deltaWall, cores);

        Assert.Null(cpu);
    }

    [Fact]
    public void Calculate_NegativeCpuTime_ReturnsNull()
    {
        TimeSpan deltaCpu = TimeSpan.FromMilliseconds(-50);
        TimeSpan deltaWall = TimeSpan.FromSeconds(1);
        int cores = 8;

        double? cpu = ProcessCpuCalculator.Calculate(deltaCpu, deltaWall, cores);

        Assert.Null(cpu);
    }

    [Fact]
    public void Calculate_ZeroOrNegativeCores_ReturnsNull()
    {
        TimeSpan deltaCpu = TimeSpan.FromMilliseconds(100);
        TimeSpan deltaWall = TimeSpan.FromSeconds(1);

        Assert.Null(ProcessCpuCalculator.Calculate(deltaCpu, deltaWall, 0));
        Assert.Null(ProcessCpuCalculator.Calculate(deltaCpu, deltaWall, -4));
    }

    [Fact]
    public void Calculate_ExcessiveCpu_ClampsToOneHundredPercent()
    {
        // Delta CPU larger than total wall capacity (e.g. clock jitter or timing edge case)
        TimeSpan deltaCpu = TimeSpan.FromSeconds(10);
        TimeSpan deltaWall = TimeSpan.FromSeconds(1);
        int cores = 2; // Max capacity 2 seconds

        double? cpu = ProcessCpuCalculator.Calculate(deltaCpu, deltaWall, cores);

        Assert.NotNull(cpu);
        Assert.Equal(100.0, cpu.Value);
    }
}
