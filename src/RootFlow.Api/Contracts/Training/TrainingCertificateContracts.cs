namespace RootFlow.Api.Contracts.Training;

public sealed record TrainingCertificateSummaryResponse(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    DateTime IssuedAtUtc,
    string Code,
    string VerificationUrl);

public sealed record PublicCertificateVerificationResponse(
    bool IsValid,
    string? EmployeeName,
    string? ProgramName,
    string? WorkspaceName,
    DateTime? IssuedAtUtc,
    string? Code);
