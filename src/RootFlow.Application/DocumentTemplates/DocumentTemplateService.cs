using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.DocumentTemplates.Commands;
using RootFlow.Application.DocumentTemplates.Dtos;
using RootFlow.Application.DocumentTemplates.Queries;
using RootFlow.Domain.DocumentTemplates;

namespace RootFlow.Application.DocumentTemplates;

public sealed class DocumentTemplateService
{
    private readonly IDocumentTemplateRepository _repository;
    private readonly IDocumentRenderer _renderer;
    private readonly IClock _clock;

    public DocumentTemplateService(
        IDocumentTemplateRepository repository,
        IDocumentRenderer renderer,
        IClock clock)
    {
        _repository = repository;
        _renderer = renderer;
        _clock = clock;
    }

    public async Task<DocumentTemplateSummaryDto> CreateAsync(
        CreateDocumentTemplateCommand command,
        CancellationToken cancellationToken = default)
    {
        var slugExists = await _repository.SlugExistsAsync(command.Slug, command.WorkspaceId, cancellationToken);
        if (slugExists)
        {
            throw new InvalidOperationException($"A template with slug '{command.Slug}' already exists in this workspace.");
        }

        var template = new DocumentTemplate(
            Guid.NewGuid(),
            command.WorkspaceId,
            command.Name,
            command.Slug,
            command.Description,
            command.Body,
            _clock.UtcNow);

        foreach (var f in command.Fields)
        {
            var fieldType = ParseFieldType(f.Type);
            template.AddField(new TemplateField(f.Key, f.Label, fieldType, f.IsRequired));
        }

        await _repository.AddAsync(template, cancellationToken);
        return ToSummaryDto(template);
    }

    public async Task<IReadOnlyList<DocumentTemplateSummaryDto>> ListAsync(
        ListDocumentTemplatesQuery query,
        CancellationToken cancellationToken = default)
    {
        var templates = await _repository.ListByWorkspaceAsync(query.WorkspaceId, cancellationToken);
        return templates.Select(ToSummaryDto).ToList();
    }

    public async Task<DocumentTemplateDetailDto?> GetByIdAsync(
        GetDocumentTemplateByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(query.TemplateId, query.WorkspaceId, cancellationToken);
        return template is null ? null : ToDetailDto(template);
    }

    public async Task<byte[]> GenerateAsync(
        GenerateDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetByIdAsync(command.TemplateId, command.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"Template {command.TemplateId} not found.");

        if (!template.IsActive)
        {
            throw new InvalidOperationException($"Template '{template.Name}' is not active.");
        }

        var missingRequired = template.Fields
            .Where(f => f.IsRequired && !command.FieldValues.ContainsKey(f.Key))
            .Select(f => f.Key)
            .ToList();

        if (missingRequired.Count > 0)
        {
            throw new ArgumentException($"Missing required fields: {string.Join(", ", missingRequired)}");
        }

        var renderRequest = new DocumentRenderRequest(
            template.Name,
            template.Body,
            command.FieldValues);

        return await _renderer.RenderAsync(renderRequest, cancellationToken);
    }

    private static TemplateFieldType ParseFieldType(string type) => type.ToUpperInvariant() switch
    {
        "DATE" => TemplateFieldType.Date,
        "NUMBER" => TemplateFieldType.Number,
        _ => TemplateFieldType.Text,
    };

    private static TemplateFieldDto ToFieldDto(TemplateField f) =>
        new(f.Key, f.Label, f.Type.ToString(), f.IsRequired);

    private static DocumentTemplateSummaryDto ToSummaryDto(DocumentTemplate t) =>
        new(t.Id, t.WorkspaceId, t.Name, t.Slug, t.Description, t.IsActive,
            t.Fields.Select(ToFieldDto).ToList(),
            t.CreatedAtUtc, t.UpdatedAtUtc);

    private static DocumentTemplateDetailDto ToDetailDto(DocumentTemplate t) =>
        new(t.Id, t.WorkspaceId, t.Name, t.Slug, t.Description, t.Body, t.IsActive,
            t.Fields.Select(ToFieldDto).ToList(),
            t.CreatedAtUtc, t.UpdatedAtUtc);
}
