namespace RootFlow.Api.Contracts.DocumentTemplates;

public sealed record TemplateFieldRequest(
    string Key,
    string Label,
    string Type,
    bool IsRequired);

public sealed record CreateDocumentTemplateRequest(
    string Name,
    string Slug,
    string? Description,
    string Body,
    TemplateFieldRequest[] Fields);
