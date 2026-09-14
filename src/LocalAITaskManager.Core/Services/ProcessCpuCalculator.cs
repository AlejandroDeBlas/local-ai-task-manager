namespace LocalAITaskManager.Core.Services;

public static class ProcessCpuCalculator
{
    public static double? Calculate(TimeSpan deltaCpu, TimeSpan deltaWall, int logicalProcessorCount)
    {
        if (logicalProcessorCount <= 0)
        {
            return null;
        }

        if (deltaWall <= TimeSpan.Zero)
        {
            return null;
        }

        if (deltaCpu < TimeSpan.Zero)
        {
            return null;
        }

        double totalCpuMs = deltaCpu.TotalMilliseconds;
        double totalWallMs = deltaWall.TotalMilliseconds * logicalProcessorCount;

        if (totalWallMs <= 0.0)
        {
            return null;
        }

        double percent = (totalCpuMs / totalWallMs) * 100.0;

        if (double.IsNaN(percent) || double.IsInfinity(percent))
        {
            return null;
        }

        return Math.Clamp(percent, 0.0, 100.0);
    }
}
