namespace RootFlow.Application.Abstractions.Documents;

public interface ITrainingCertificateRenderer
{
    byte[] Render(TrainingCertificateRenderRequest request);
}

public sealed record TrainingCertificateRenderRequest(
    string EmployeeName,
    string ProgramName,
    string? ProgramDescription,
    string WorkspaceName,
    string CertificateCode,
    string VerificationUrl,
    DateTime IssuedAtUtc);
