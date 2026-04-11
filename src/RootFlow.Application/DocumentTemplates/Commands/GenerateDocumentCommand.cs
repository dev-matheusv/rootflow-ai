namespace RootFlow.Application.DocumentTemplates.Commands;

public sealed record GenerateDocumentCommand(
    Guid TemplateId,
    Guid WorkspaceId,
    IReadOnlyDictionary<string, string> FieldValues);
