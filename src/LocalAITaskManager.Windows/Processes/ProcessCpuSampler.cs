using LocalAITaskManager.Core.Abstractions;
using LocalAITaskManager.Core.Services;

namespace LocalAITaskManager.Windows.Processes;

public sealed class ProcessCpuSampler : IProcessCpuSampler
{
    private sealed record CpuSampleState(
        TimeSpan TotalProcessorTime,
        DateTimeOffset Timestamp
    );

    private readonly Dictionary<int, CpuSampleState> _states = [];
    private readonly object _syncLock = new();

    public double? SampleCpu(int pid, TimeSpan totalProcessorTime, DateTimeOffset timestamp)
    {
        lock (_syncLock)
        {
            if (!_states.TryGetValue(pid, out CpuSampleState? previous))
            {
                _states[pid] = new CpuSampleState(totalProcessorTime, timestamp);
                return null;
            }

            TimeSpan deltaCpu = totalProcessorTime - previous.TotalProcessorTime;
            TimeSpan deltaWall = timestamp - previous.Timestamp;

            _states[pid] = new CpuSampleState(totalProcessorTime, timestamp);

            if (deltaCpu < TimeSpan.Zero)
            {
                // Process may have been restarted with same PID
                return null;
            }

            return ProcessCpuCalculator.Calculate(deltaCpu, deltaWall, Environment.ProcessorCount);
        }
    }

    public void Cleanup(IReadOnlyCollection<int> currentPids)
    {
        lock (_syncLock)
        {
            var pidsToRemove = new List<int>();
            foreach (int pid in _states.Keys)
            {
                if (!currentPids.Contains(pid))
                {
                    pidsToRemove.Add(pid);
                }
            }

            foreach (int pid in pidsToRemove)
            {
                _states.Remove(pid);
            }
        }
    }
}
