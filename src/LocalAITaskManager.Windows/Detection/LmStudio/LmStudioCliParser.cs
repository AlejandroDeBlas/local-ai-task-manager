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
    public static List<LmStudioLoadedModel> ParseModels(string? json)
    {
        var models = new List<LmStudioLoadedModel>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return models;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement elem in root.EnumerateArray())
                {
                    if (elem.ValueKind == JsonValueKind.Object)
                    {
                        var m = ParseModelElement(elem);
                        if (m is not null)
                        {
                            models.Add(m);
                        }
                    }
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                // Check common list properties: "models", "data", "loadedModels", "items"
                foreach (string propName in new[] { "models", "data", "loadedModels", "items" })
                {
                    if (root.TryGetProperty(propName, out JsonElement listElem) && listElem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement elem in listElem.EnumerateArray())
                        {
                            if (elem.ValueKind == JsonValueKind.Object)
                            {
                                var m = ParseModelElement(elem);
                                if (m is not null)
                                {
                                    models.Add(m);
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }
        catch
        {
            // Invalid or unexpected JSON structure - return empty list gracefully
        }

        return models;
    }

    private static LmStudioLoadedModel? ParseModelElement(JsonElement elem)
    {
        string? identifier = GetStringProperty(elem, "identifier", "id", "name");
        string? path = GetStringProperty(elem, "path", "modelPath", "file");
        string? modelKey = GetStringProperty(elem, "modelKey", "model");
        int? context = GetIntProperty(elem, "contextLength", "context_length", "context");
        ulong? size = GetUlongProperty(elem, "sizeBytes", "size_bytes", "size");

        if (identifier is null && path is null && modelKey is null)
        {
            return null;
        }

        return new LmStudioLoadedModel
        {
            Identifier = identifier,
            Path = path,
            ModelKey = modelKey,
            ContextLength = context,
            SizeBytes = size
        };
    }

    private static string? GetStringProperty(JsonElement elem, params string[] names)
    {
        foreach (string name in names)
        {
            if (elem.TryGetProperty(name, out JsonElement prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
        }
        return null;
    }

    private static int? GetIntProperty(JsonElement elem, params string[] names)
    {
        foreach (string name in names)
        {
            if (elem.TryGetProperty(name, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out int val))
                {
                    return val;
                }
            }
        }
        return null;
    }

    private static ulong? GetUlongProperty(JsonElement elem, params string[] names)
    {
        foreach (string name in names)
        {
            if (elem.TryGetProperty(name, out JsonElement prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetUInt64(out ulong val))
                {
                    return val;
                }
            }
        }
        return null;
    }
}
