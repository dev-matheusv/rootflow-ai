using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Documents.Commands;
using RootFlow.Application.Documents.Dtos;
using RootFlow.Application.Documents.Queries;
using RootFlow.Domain.Knowledge;

namespace RootFlow.Application.Documents;

public sealed class DocumentService
{
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IKnowledgeDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly ITextChunker _textChunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IClock _clock;

    public DocumentService(
        IWorkspaceRepository workspaceRepository,
        IKnowledgeDocumentRepository documentRepository,
        IDocumentChunkRepository chunkRepository,
        IFileStorage fileStorage,
        IDocumentTextExtractor textExtractor,
        ITextChunker textChunker,
        IEmbeddingService embeddingService,
        IClock clock)
    {
        _workspaceRepository = workspaceRepository;
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _fileStorage = fileStorage;
        _textExtractor = textExtractor;
        _textChunker = textChunker;
        _embeddingService = embeddingService;
        _clock = clock;
    }

    public async Task<DocumentDto> UploadAsync(
        UploadDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var workspaceExists = await _workspaceRepository.ExistsAsync(command.WorkspaceId, cancellationToken);
        if (!workspaceExists)
        {
            throw new InvalidOperationException("Workspace was not found.");
        }

        var documentId = Guid.NewGuid();
        var createdAtUtc = _clock.UtcNow;
        var storedFile = await _fileStorage.SaveAsync(command.File, cancellationToken);
        var checksum = CreateChecksum(storedFile);

        var document = new KnowledgeDocument(
            documentId,
            command.WorkspaceId,
            storedFile.FileName,
            storedFile.ContentType,
            storedFile.SizeBytes,
            storedFile.StoragePath,
            checksum,
            createdAtUtc);

        await _documentRepository.AddAsync(document, cancellationToken);

        try
        {
            document.MarkProcessing();
            await _documentRepository.UpdateAsync(document, cancellationToken);

            var extractedText = await _textExtractor.ExtractTextAsync(storedFile, cancellationToken);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                throw new InvalidOperationException("The uploaded file did not contain readable text.");
            }

            var chunks = _textChunker.Chunk(extractedText);
            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("The uploaded file did not produce searchable chunks.");
            }

            var embeddingInputs = chunks
                .Select(chunk => BuildEmbeddingInput(storedFile.FileName, chunk))
                .ToArray();

            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                embeddingInputs,
                cancellationToken);

            var documentChunks = new List<DocumentChunk>(chunks.Count);
            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var documentChunk = new DocumentChunk(
                    Guid.NewGuid(),
                    command.WorkspaceId,
                    document.Id,
                    chunk.Sequence,
                    chunk.Content,
                    chunk.TokenCount,
                    chunk.SourceLabel,
                    _clock.UtcNow);

                documentChunk.SetEmbedding(embeddings[i]);
                documentChunks.Add(documentChunk);
            }

            await _chunkRepository.AddRangeAsync(documentChunks, cancellationToken);

            document.MarkProcessed(extractedText, _clock.UtcNow);
            await _documentRepository.UpdateAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            document.MarkFailed(ex.Message);
            await _documentRepository.UpdateAsync(document, cancellationToken);
        }

        return Map(document);
    }

    public async Task<DocumentDto?> GetByIdAsync(
        GetDocumentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByIdAsync(
            query.WorkspaceId,
            query.DocumentId,
            cancellationToken);

        return document is null ? null : Map(document);
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(
        ListDocumentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.ListByWorkspaceAsync(query.WorkspaceId, cancellationToken);
        return documents.Select(Map).ToArray();
    }

    private static DocumentDto Map(KnowledgeDocument document)
    {
        return new DocumentDto(
            document.Id,
            document.WorkspaceId,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Status,
            document.CreatedAtUtc,
            document.ProcessedAtUtc,
            document.FailureReason);
    }

    private static string CreateChecksum(StoredFile file)
    {
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{file.StoragePath}|{file.FileName}|{file.SizeBytes}|{file.ContentType}")));
    }

    private static string BuildEmbeddingInput(string documentName, TextChunk chunk)
    {
        return $$"""
                 Document: {{documentName}}
                 Section: {{chunk.SourceLabel}}
                 Content:
                 {{chunk.Content}}
                 """;
    }
}
