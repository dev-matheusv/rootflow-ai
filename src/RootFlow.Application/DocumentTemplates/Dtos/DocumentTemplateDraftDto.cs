namespace RootFlow.Application.DocumentTemplates.Dtos;

public sealed record DocumentTemplateDraftDto(
    string Name,
    string Body,
    IReadOnlyList<TemplateFieldDto> Fields);
