namespace RootFlow.Api.Contracts.DocumentTemplates;

public sealed record TemplateFieldResponse(
    string Key,
    string Label,
    string Type,
    bool IsRequired);

public sealed record DocumentTemplateSummaryResponse(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    TemplateFieldResponse[] Fields,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DocumentTemplateDetailResponse(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Slug,
    string? Description,
    string Body,
    bool IsActive,
    TemplateFieldResponse[] Fields,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
