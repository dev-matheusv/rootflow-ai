namespace RootFlow.Application.DocumentTemplates.Queries;

public sealed record GetDocumentTemplateByIdQuery(Guid TemplateId, Guid WorkspaceId);
