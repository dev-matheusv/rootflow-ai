namespace RootFlow.Application.DocumentTemplates.Commands;

public sealed record CreateTemplateFieldCommand(
    string Key,
    string Label,
    string Type,
    bool IsRequired);

public sealed record CreateDocumentTemplateCommand(
    Guid WorkspaceId,
    string Name,
    string? Description,
    string Body,
    IReadOnlyList<CreateTemplateFieldCommand> Fields,
    string? Slug = null);
