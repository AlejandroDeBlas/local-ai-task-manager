namespace LocalAITaskManager.Core.Models;

/// <summary>
/// Informative diagnostic warning or failure message from telemetry providers.
/// </summary>
public sealed record TelemetryWarning(
    string Source,
    string Message,
    DateTimeOffset Timestamp
);
