namespace RootFlow.Application.DocumentTemplates.Dtos;

public sealed record TemplateFieldDto(
    string Key,
    string Label,
    string Type,
    bool IsRequired);

public sealed record DocumentTemplateSummaryDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    IReadOnlyList<TemplateFieldDto> Fields,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DocumentTemplateDetailDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Slug,
    string? Description,
    string Body,
    bool IsActive,
    IReadOnlyList<TemplateFieldDto> Fields,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
