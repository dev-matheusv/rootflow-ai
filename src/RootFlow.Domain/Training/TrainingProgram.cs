namespace RootFlow.Domain.Training;

public sealed class TrainingProgram
{
    private TrainingProgram()
    {
    }

    public TrainingProgram(
        Guid id,
        Guid workspaceId,
        string name,
        string slug,
        string? description,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Training program id cannot be empty.", nameof(id));
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creator user id cannot be empty.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        Id = id;
        WorkspaceId = workspaceId;
        Name = name.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PassingScore = 70;
        IsPublished = false;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public string? Description { get; private set; }
    public int PassingScore { get; private set; }
    public bool IsPublished { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public void UpdateDetails(
        string name,
        string? description,
        int passingScore,
        DateTime updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (passingScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(passingScore), "Passing score must be between 0 and 100.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        PassingScore = passingScore;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Publish(DateTime updatedAtUtc)
    {
        IsPublished = true;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Unpublish(DateTime updatedAtUtc)
    {
        IsPublished = false;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static TrainingProgram Rehydrate(
        Guid id,
        Guid workspaceId,
        string name,
        string slug,
        string? description,
        int passingScore,
        bool isPublished,
        Guid createdByUserId,
        DateTime createdAtUtc,
        DateTime updatedAtUtc)
    {
        return new TrainingProgram
        {
            Id = id,
            WorkspaceId = workspaceId,
            Name = name,
            Slug = slug,
            Description = description,
            PassingScore = passingScore,
            IsPublished = isPublished,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        };
    }
}
