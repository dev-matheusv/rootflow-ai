namespace RootFlow.Domain.Training;

public sealed class TrainingModule
{
    private readonly List<Guid> _sourceDocumentIds = [];

    private TrainingModule()
    {
    }

    public TrainingModule(
        Guid id,
        Guid programId,
        int orderIndex,
        string title,
        string? description,
        IEnumerable<Guid> sourceDocumentIds,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Module id cannot be empty.", nameof(id));
        if (programId == Guid.Empty) throw new ArgumentException("Program id cannot be empty.", nameof(programId));
        if (orderIndex < 0) throw new ArgumentOutOfRangeException(nameof(orderIndex), "Order must be non-negative.");
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Id = id;
        ProgramId = programId;
        OrderIndex = orderIndex;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        _sourceDocumentIds.AddRange(sourceDocumentIds ?? []);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProgramId { get; private set; }
    public int OrderIndex { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public IReadOnlyList<Guid> SourceDocumentIds => _sourceDocumentIds.AsReadOnly();

    public void UpdateDetails(
        string title,
        string? description,
        int orderIndex,
        IEnumerable<Guid> sourceDocumentIds,
        DateTime updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (orderIndex < 0) throw new ArgumentOutOfRangeException(nameof(orderIndex), "Order must be non-negative.");

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        OrderIndex = orderIndex;
        _sourceDocumentIds.Clear();
        _sourceDocumentIds.AddRange(sourceDocumentIds ?? []);
        UpdatedAtUtc = updatedAtUtc;
    }

    public static TrainingModule Rehydrate(
        Guid id,
        Guid programId,
        int orderIndex,
        string title,
        string? description,
        IEnumerable<Guid> sourceDocumentIds,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        var module = new TrainingModule
        {
            Id = id,
            ProgramId = programId,
            OrderIndex = orderIndex,
            Title = title,
            Description = description,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
        module._sourceDocumentIds.AddRange(sourceDocumentIds ?? []);
        return module;
    }
}
