namespace RootFlow.Infrastructure.Configuration;

public sealed class OpenAiOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";

    public string? ApiKey { get; set; }

    public string ChatModel { get; set; } = "gpt-4.1-mini";

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
}
