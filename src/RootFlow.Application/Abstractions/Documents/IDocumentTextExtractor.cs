namespace RootFlow.Application.Abstractions.Documents;

public interface IDocumentTextExtractor
{
    Task<string> ExtractTextAsync(StoredFile file, CancellationToken cancellationToken = default);
}
