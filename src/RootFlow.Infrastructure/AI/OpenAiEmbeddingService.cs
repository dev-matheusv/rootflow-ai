using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.AI;

public sealed class OpenAiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;

    public OpenAiEmbeddingService(HttpClient httpClient, IOptions<OpenAiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await GenerateEmbeddingsAsync([text], cancellationToken);
        return results[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs.Count == 0)
        {
            return Array.Empty<float[]>();
        }

        EnsureConfigured();

        using var response = await _httpClient.PostAsJsonAsync(
            "embeddings",
            new
            {
                model = _options.EmbeddingModel,
                input = inputs
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        var data = document.RootElement.GetProperty("data")
            .EnumerateArray()
            .OrderBy(x => x.GetProperty("index").GetInt32())
            .ToArray();

        var embeddings = new List<float[]>(data.Length);
        foreach (var item in data)
        {
            embeddings.Add(item.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray());
        }

        return embeddings;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.EmbeddingModel))
        {
            throw new InvalidOperationException("OpenAI embedding model is not configured.");
        }
    }
}
