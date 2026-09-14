using LocalAITaskManager.Windows.Processes;
using Xunit;

namespace LocalAITaskManager.Windows.Tests;

public class ProcessCpuSamplerTests
{
    [Fact]
    public void SampleCpu_FirstSample_ReturnsNull()
    {
        var sampler = new ProcessCpuSampler();
        int pid = 1234;
        var time = TimeSpan.FromSeconds(10);
        var now = DateTimeOffset.UtcNow;

        double? cpu = sampler.SampleCpu(pid, time, now);

        Assert.Null(cpu);
    }

    [Fact]
    public void SampleCpu_SecondSample_CalculatesCpuDelta()
    {
        var sampler = new ProcessCpuSampler();
        int pid = 1234;
        var t0 = DateTimeOffset.UtcNow;
        var cpu0 = TimeSpan.FromSeconds(10);

        sampler.SampleCpu(pid, cpu0, t0);

        var t1 = t0.AddSeconds(1);
        var cpu1 = cpu0 + TimeSpan.FromMilliseconds(500); // 0.5s CPU in 1.0s wall clock

        double? cpu = sampler.SampleCpu(pid, cpu1, t1);

        Assert.NotNull(cpu);
        Assert.True(cpu.Value > 0.0);
    }

    [Fact]
    public void Cleanup_RemovesOldPids()
    {
        var sampler = new ProcessCpuSampler();
        var t0 = DateTimeOffset.UtcNow;

        sampler.SampleCpu(101, TimeSpan.FromSeconds(1), t0);
        sampler.SampleCpu(102, TimeSpan.FromSeconds(1), t0);

        // Active PIDs only contains 101; 102 should be removed
        sampler.Cleanup([101]);

        // When 102 is sampled again, it should be treated as a first sample (null)
        double? result = sampler.SampleCpu(102, TimeSpan.FromSeconds(2), t0.AddSeconds(1));
        Assert.Null(result);
    }
}
