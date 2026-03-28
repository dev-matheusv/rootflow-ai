using RootFlow.Domain.Knowledge;

namespace RootFlow.Application.Documents.Dtos;

public sealed record DocumentDto(
    Guid Id,
    Guid WorkspaceId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DocumentStatus Status,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    string? FailureReason);
