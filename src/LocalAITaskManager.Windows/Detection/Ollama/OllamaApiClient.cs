using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalAITaskManager.Windows.Detection.Ollama;

public sealed class OllamaApiClient : IOllamaApiClient, IDisposable
{
    private const string PsEndpoint = "http://127.0.0.1:11434/api/ps";
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OllamaApiClient(HttpClient? httpClient = null)
    {
        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _disposeClient = false;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromMilliseconds(500)
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(500)
            };
            _disposeClient = true;
        }
    }

    public async Task<OllamaPsResponse?> GetLoadedModelsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(PsEndpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<OllamaPsResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Transient timeout, connection refused, or parsing failure
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }
}
