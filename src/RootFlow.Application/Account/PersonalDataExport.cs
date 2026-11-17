namespace RootFlow.Application.Account;

// RF-13 (LGPD art. 18, II and V): everything RootFlow holds about the person, in a portable format.
// Workspace documents belong to the company (the controller), not to the person, so they are not included.
public sealed record PersonalDataExport(
    DateTime GeneratedAtUtc,
    ExportedProfile Profile,
    IReadOnlyList<ExportedMembership> Memberships,
    IReadOnlyList<ExportedConversation> Conversations,
    IReadOnlyList<ExportedTrainingAttempt> TrainingAttempts,
    IReadOnlyList<ExportedCertificate> Certificates);

public sealed record ExportedProfile(Guid Id, string FullName, string Email, DateTime CreatedAtUtc);

public sealed record ExportedMembership(string WorkspaceName, string Role, bool IsActive, DateTime JoinedAtUtc);

public sealed record ExportedConversation(
    Guid Id,
    string WorkspaceName,
    string Title,
    DateTime CreatedAtUtc,
    IReadOnlyList<ExportedMessage> Messages);

public sealed record ExportedMessage(string Role, string Content, DateTime CreatedAtUtc);

public sealed record ExportedTrainingAttempt(
    string WorkspaceName,
    string ModuleTitle,
    string Status,
    int? Score,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record ExportedCertificate(string WorkspaceName, string ProgramName, string Code, DateTime IssuedAtUtc);
