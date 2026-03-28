namespace RootFlow.Application.Documents.Queries;

public sealed record GetDocumentByIdQuery(Guid WorkspaceId, Guid DocumentId);
