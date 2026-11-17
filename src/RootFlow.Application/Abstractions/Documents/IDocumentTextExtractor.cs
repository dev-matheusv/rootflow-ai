namespace RootFlow.Application.Abstractions.Documents;

public interface IDocumentTextExtractor
{
    Task<string> ExtractTextAsync(StoredFile file, CancellationToken cancellationToken = default);

    /// <summary>RF-06: true when extracting this file calls an AI model and therefore costs credits.</summary>
    bool RequiresAi(StoredFile file) => false;

    /// <summary>RF-06: text plus the AI usage spent to get it.</summary>
    async Task<DocumentExtraction> ExtractAsync(StoredFile file, CancellationToken cancellationToken = default)
        => new(await ExtractTextAsync(file, cancellationToken), null);
}
