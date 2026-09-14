using System.Text.Json.Serialization;

namespace LocalAITaskManager.Windows.Detection.Ollama;

public sealed class OllamaPsResponse
{
    [JsonPropertyName("models")]
    public List<OllamaModelItem>? Models { get; set; }
}

public sealed class OllamaModelItem
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("size")]
    public ulong? Size { get; set; }

    [JsonPropertyName("size_vram")]
    public ulong? SizeVram { get; set; }

    [JsonPropertyName("context_length")]
    public int? ContextLength { get; set; }

    [JsonPropertyName("details")]
    public OllamaModelDetails? Details { get; set; }
}

public sealed class OllamaModelDetails
{
    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("parameter_size")]
    public string? ParameterSize { get; set; }

    [JsonPropertyName("quantization_level")]
    public string? QuantizationLevel { get; set; }
}
