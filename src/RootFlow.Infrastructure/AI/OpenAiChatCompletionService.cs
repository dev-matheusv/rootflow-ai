using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Domain.Conversations;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.AI;

public sealed class OpenAiChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;

    public OpenAiChatCompletionService(HttpClient httpClient, IOptions<OpenAiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var payload = new
        {
            model = _options.ChatModel,
            temperature = 0.2,
            messages = request.Messages.Select(x => new
            {
                role = ToRoleName(x.Role),
                content = x.Content
            })
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        var modelName = root.TryGetProperty("model", out var modelProperty)
            ? modelProperty.GetString()
            : null;

        var content = root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("The chat completion response did not include any content.");
        }

        return new ChatCompletionResponse(content.Trim(), modelName);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_options.ChatModel))
        {
            throw new InvalidOperationException("OpenAI chat model is not configured.");
        }
    }

    private static string ToRoleName(MessageRole role)
    {
        return role switch
        {
            MessageRole.System => "system",
            MessageRole.User => "user",
            MessageRole.Assistant => "assistant",
            _ => "user"
        };
    }
}
