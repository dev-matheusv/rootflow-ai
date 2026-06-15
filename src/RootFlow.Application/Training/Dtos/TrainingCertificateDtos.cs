namespace RootFlow.Application.Training.Dtos;

public sealed record TrainingCertificateSummaryDto(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    DateTime IssuedAtUtc,
    string Code,
    string VerificationUrl);

public sealed record TrainingCertificatePdfDto(
    string FileName,
    byte[] Content);

public sealed record PublicCertificateVerificationDto(
    bool IsValid,
    string? EmployeeName,
    string? ProgramName,
    string? WorkspaceName,
    DateTime? IssuedAtUtc,
    string? Code);
