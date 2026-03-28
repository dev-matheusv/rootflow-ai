using RootFlow.Application.Abstractions.Documents;

namespace RootFlow.Application.Documents.Commands;

public sealed record UploadDocumentCommand(Guid WorkspaceId, FileUpload File);
