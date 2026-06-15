namespace RootFlow.Domain.Training;

public sealed class TrainingCertificate
{
    private TrainingCertificate()
    {
    }

    public TrainingCertificate(
        Guid id,
        Guid programId,
        Guid userId,
        Guid workspaceId,
        string code,
        string pdfStorageKey,
        DateTime issuedAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("Certificate id cannot be empty.", nameof(id));
        if (programId == Guid.Empty) throw new ArgumentException("Program id cannot be empty.", nameof(programId));
        if (userId == Guid.Empty) throw new ArgumentException("User id cannot be empty.", nameof(userId));
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfStorageKey);

        Id = id;
        ProgramId = programId;
        UserId = userId;
        WorkspaceId = workspaceId;
        Code = code.Trim().ToUpperInvariant();
        PdfStorageKey = pdfStorageKey.Trim();
        IssuedAtUtc = issuedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProgramId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Code { get; private set; } = null!;
    public string PdfStorageKey { get; private set; } = null!;
    public DateTime IssuedAtUtc { get; private set; }

    public static TrainingCertificate Rehydrate(
        Guid id,
        Guid programId,
        Guid userId,
        Guid workspaceId,
        string code,
        string pdfStorageKey,
        DateTime issuedAtUtc)
    {
        return new TrainingCertificate
        {
            Id = id,
            ProgramId = programId,
            UserId = userId,
            WorkspaceId = workspaceId,
            Code = code,
            PdfStorageKey = pdfStorageKey,
            IssuedAtUtc = issuedAtUtc,
        };
    }
}
