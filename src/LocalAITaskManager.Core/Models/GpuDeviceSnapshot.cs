namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Immutable snapshot representing the hardware and telemetry state of a single GPU device.
/// Optional metrics and memory fields are null when unsupported or unavailable.
/// </summary>
public sealed record GpuDeviceSnapshot(
    string Id,
    int Index,
    string Name,
    ulong? TotalVramBytes,
    ulong? UsedVramBytes,
    double? GpuUtilizationPercent,
    double? TemperatureCelsius,
    double? PowerWatts,
    string? DriverVersion
);
