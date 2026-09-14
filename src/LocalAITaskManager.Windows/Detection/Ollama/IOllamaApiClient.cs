namespace LocalAITaskManager.Windows.Detection.Ollama;

public interface IOllamaApiClient
{
    Task<OllamaPsResponse?> GetLoadedModelsAsync(CancellationToken cancellationToken);
}
