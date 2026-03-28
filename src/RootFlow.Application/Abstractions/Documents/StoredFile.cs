namespace RootFlow.Application.Abstractions.Documents;

public sealed record StoredFile(
    string StoragePath,
    string FileName,
    string ContentType,
    long SizeBytes);
