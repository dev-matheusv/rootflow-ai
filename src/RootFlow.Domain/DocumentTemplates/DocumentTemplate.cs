namespace RootFlow.Domain.DocumentTemplates;

public sealed class DocumentTemplate
{
    private readonly List<TemplateField> _fields = [];

    private DocumentTemplate()
    {
    }

    public DocumentTemplate(
        Guid id,
        Guid workspaceId,
        string name,
        string slug,
        string? description,
        string body,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Template id cannot be empty.", nameof(id));
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        Id = id;
        WorkspaceId = workspaceId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Description = description?.Trim();
        Body = body;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Body { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<TemplateField> Fields => _fields.AsReadOnly();

    public void AddField(TemplateField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentException.ThrowIfNullOrWhiteSpace(field.Key);
        _fields.Add(field);
    }

    public void SetFields(IEnumerable<TemplateField> fields, DateTime updatedAtUtc)
    {
        _fields.Clear();
        foreach (var field in fields)
        {
            _fields.Add(field);
        }
        UpdatedAtUtc = updatedAtUtc;
    }

    public void UpdateBody(string body, DateTime updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        Body = body;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Deactivate(DateTime updatedAtUtc)
    {
        IsActive = false;
        UpdatedAtUtc = updatedAtUtc;
    }

    // Used by the repository to rehydrate from DB
    public static DocumentTemplate Rehydrate(
        Guid id,
        Guid workspaceId,
        string name,
        string slug,
        string? description,
        string body,
        bool isActive,
        IEnumerable<TemplateField> fields,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        var template = new DocumentTemplate
        {
            Id = id,
            WorkspaceId = workspaceId,
            Name = name,
            Slug = slug,
            Description = description,
            Body = body,
            IsActive = isActive,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
        template._fields.AddRange(fields);
        return template;
    }
}
