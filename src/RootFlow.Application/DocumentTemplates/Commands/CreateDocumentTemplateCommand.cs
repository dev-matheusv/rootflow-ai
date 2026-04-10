namespace RootFlow.Application.DocumentTemplates.Commands;

public sealed record CreateTemplateFieldCommand(
    string Key,
    string Label,
    string Type,
    bool IsRequired);

public sealed record CreateDocumentTemplateCommand(
    Guid WorkspaceId,
    string Name,
    string Slug,
    string? Description,
    string Body,
    IReadOnlyList<CreateTemplateFieldCommand> Fields);
