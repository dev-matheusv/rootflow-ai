namespace RootFlow.Domain.Training;

public sealed class TrainingAttempt
{
    private TrainingAttempt()
    {
    }

    public TrainingAttempt(
        Guid id,
        Guid moduleId,
        Guid userId,
        Guid workspaceId,
        DateTime startedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Attempt id cannot be empty.", nameof(id));
        if (moduleId == Guid.Empty) throw new ArgumentException("Module id cannot be empty.", nameof(moduleId));
        if (userId == Guid.Empty) throw new ArgumentException("User id cannot be empty.", nameof(userId));
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));

        Id = id;
        ModuleId = moduleId;
        UserId = userId;
        WorkspaceId = workspaceId;
        StartedAtUtc = startedAtUtc;
        Status = TrainingAttemptStatus.InProgress;
    }

    public Guid Id { get; private set; }
    public Guid ModuleId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public int? Score { get; private set; }
    public TrainingAttemptStatus Status { get; private set; }

    public void Complete(int score, int passingScore, DateTime completedAtUtc)
    {
        if (Status != TrainingAttemptStatus.InProgress)
        {
            throw new InvalidOperationException("Only in-progress attempts can be completed.");
        }

        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");
        }

        Score = score;
        CompletedAtUtc = completedAtUtc;
        Status = score >= passingScore ? TrainingAttemptStatus.Passed : TrainingAttemptStatus.Failed;
    }

    public static TrainingAttempt Rehydrate(
        Guid id,
        Guid moduleId,
        Guid userId,
        Guid workspaceId,
        DateTime startedAtUtc,
        DateTime? completedAtUtc,
        int? score,
        TrainingAttemptStatus status)
    {
        return new TrainingAttempt
        {
            Id = id,
            ModuleId = moduleId,
            UserId = userId,
            WorkspaceId = workspaceId,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Score = score,
            Status = status,
        };
    }
}
