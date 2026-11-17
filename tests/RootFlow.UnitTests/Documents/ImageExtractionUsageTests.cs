using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.Documents;

namespace RootFlow.UnitTests.Documents;

// RF-06: reading an image calls the model, so the extractor must report the tokens to be charged.
public sealed class ImageExtractionUsageTests
{
    [Fact]
    public async Task ExtractAsync_Image_ReturnsTextAndUsageWithConfiguredModel()
    {
        var handler = new StubHandler("""
            {
              "model": "gpt-4.1-mini-2025-04-14",
              "choices": [ { "message": { "content": "  Contract 123  " } } ],
              "usage": { "prompt_tokens": 1200, "completion_tokens": 40, "total_tokens": 1240 }
            }
            """);
        var extractor = CreateExtractor(handler);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, [0x89, 0x50, 0x4E, 0x47]);

        try
        {
            var file = new StoredFile(path, "scan.png", "image/png", 4);

            Assert.True(extractor.RequiresAi(file));
            var result = await extractor.ExtractAsync(file);

            Assert.Equal("Contract 123", result.Text);
            Assert.NotNull(result.Usage);
            Assert.Equal("openai", result.Usage!.Provider);
            Assert.Equal("gpt-4.1-mini-2025-04-14", result.Usage.Model);
            Assert.Equal(1200, result.Usage.PromptTokens);
            Assert.Equal(40, result.Usage.CompletionTokens);
            Assert.Equal(1240, result.Usage.TotalTokens);
            Assert.Contains("\"model\":\"gpt-4.1-mini\"", handler.LastRequestBody);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractAsync_TextFile_HasNoUsage()
    {
        var extractor = CreateExtractor(new StubHandler("{}"));
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "plain text");

        try
        {
            var file = new StoredFile(path, "notes.txt", "text/plain", 10);

            Assert.False(extractor.RequiresAi(file));
            var result = await extractor.ExtractAsync(file);

            Assert.Equal("plain text", result.Text);
            Assert.Null(result.Usage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SimpleDocumentTextExtractor CreateExtractor(StubHandler handler)
    {
        var options = Options.Create(new OpenAiOptions { ApiKey = "test-key", ChatModel = "gpt-4.1-mini" });
        return new SimpleDocumentTextExtractor(new StubHttpClientFactory(handler), options);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(string responseJson) : HttpMessageHandler
    {
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
