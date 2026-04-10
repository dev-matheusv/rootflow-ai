namespace RootFlow.Domain.DocumentTemplates;

public sealed record TemplateField(
    string Key,
    string Label,
    TemplateFieldType Type,
    bool IsRequired);
