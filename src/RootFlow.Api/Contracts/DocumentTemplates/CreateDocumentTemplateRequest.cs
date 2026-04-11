namespace RootFlow.Api.Contracts.DocumentTemplates;

public sealed record AiSuggestTemplateRequest(string Description);

public sealed record TemplateFieldRequest(
    string Key,
    string Label,
    string Type,
    bool IsRequired);

public sealed record CreateDocumentTemplateRequest(
    string Name,
    string? Description,
    string Body,
    TemplateFieldRequest[] Fields,
    string? Slug = null);
