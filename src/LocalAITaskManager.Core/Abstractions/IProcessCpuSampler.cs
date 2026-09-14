namespace LocalAITaskManager.Core.Abstractions;

public interface IProcessCpuSampler
{
    double? SampleCpu(int pid, TimeSpan totalProcessorTime, DateTimeOffset timestamp);
    void Cleanup(IReadOnlyCollection<int> currentPids);
}
