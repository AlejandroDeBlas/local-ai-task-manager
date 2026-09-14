namespace LocalAITaskManager.Windows.PerformanceCounters;

public sealed class PdhTelemetryException : Exception
{
    public uint StatusCode { get; }

    public PdhTelemetryException(string message, uint statusCode)
        : base($"{message} (PDH Status: 0x{statusCode:X8})")
    {
        StatusCode = statusCode;
    }
}
