using System.Text.Json;

namespace LocalAITaskManager.Windows.Detection.LmStudio;

public sealed class LmStudioLoadedModel
{
    public string? Identifier { get; set; }
    public string? Path { get; set; }
    public string? ModelKey { get; set; }
    public int? ContextLength { get; set; }
    public ulong? SizeBytes { get; set; }
}

public static class LmStudioCliParser
{
    /// <summary>
    /// Model detection via CLI is deactivated / not validated until a stable daemon-less schema is verified.
    /// Returns an empty list to prevent speculative model attribution.
    /// </summary>
    public static List<LmStudioLoadedModel> ParseModels(string? json) => [];
}
