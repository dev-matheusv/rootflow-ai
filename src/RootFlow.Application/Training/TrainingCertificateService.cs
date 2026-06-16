using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Training.Dtos;
using RootFlow.Domain.Training;

namespace RootFlow.Application.Training;

public sealed class TrainingCertificateService
{
    private readonly ITrainingRepository _trainingRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IAuthRepository _authRepository;
    private readonly ITrainingCertificateRenderer _renderer;
    private readonly TrainingFeatureGate _featureGate;
    private readonly TrainingOptions _options;
    private readonly ILogger<TrainingCertificateService> _logger;

    public TrainingCertificateService(
        ITrainingRepository trainingRepository,
        IWorkspaceRepository workspaceRepository,
        IAuthRepository authRepository,
        ITrainingCertificateRenderer renderer,
        TrainingFeatureGate featureGate,
        TrainingOptions options,
        ILogger<TrainingCertificateService> logger)
    {
        _trainingRepository = trainingRepository;
        _workspaceRepository = workspaceRepository;
        _authRepository = authRepository;
        _renderer = renderer;
        _featureGate = featureGate;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TrainingCertificateSummaryDto>> ListForUserAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _featureGate.EnsureEnabledAsync(workspaceId, cancellationToken);
        var certs = await _trainingRepository.ListCertificatesByUserAsync(userId, cancellationToken);
        var summaries = new List<TrainingCertificateSummaryDto>(certs.Count);
        foreach (var cert in certs)
        {
            var program = await _trainingRepository.GetProgramByIdAsync(cert.ProgramId, cert.WorkspaceId, cancellationToken);
            summaries.Add(new TrainingCertificateSummaryDto(
                cert.Id,
                cert.ProgramId,
                program?.Name ?? "Programa removido",
                cert.IssuedAtUtc,
                cert.Code,
                BuildVerificationUrl(cert.Code)));
        }
        return summaries;
    }

    public async Task<TrainingCertificatePdfDto> RenderCertificatePdfAsync(
        Guid certificateId,
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        await _featureGate.EnsureEnabledAsync(workspaceId, cancellationToken);
        var cert = await _trainingRepository.GetCertificateByIdAsync(certificateId, workspaceId, cancellationToken);
        if (cert is null || cert.UserId != userId)
        {
            throw new TrainingNotFoundException($"Certificate {certificateId} was not found.");
        }

        var (employeeName, programName, programDescription, workspaceName) =
            await ResolveCertificateContextAsync(cert, cancellationToken);

        var pdf = _renderer.Render(new TrainingCertificateRenderRequest(
            employeeName,
            programName,
            programDescription,
            workspaceName,
            cert.Code,
            BuildVerificationUrl(cert.Code),
            cert.IssuedAtUtc));

        var safeName = MakeSafeFileName(programName, cert.Code);
        return new TrainingCertificatePdfDto(safeName, pdf);
    }

    public async Task<PublicCertificateVerificationDto> VerifyByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return new PublicCertificateVerificationDto(false, null, null, null, null, null);
        }

        var cert = await _trainingRepository.GetCertificateByCodeAsync(code, cancellationToken);
        if (cert is null)
        {
            _logger.LogInformation("Public verification for unknown certificate code {Code}.", code);
            return new PublicCertificateVerificationDto(false, null, null, null, null, code.Trim().ToUpperInvariant());
        }

        var (employeeName, programName, _, workspaceName) =
            await ResolveCertificateContextAsync(cert, cancellationToken);

        return new PublicCertificateVerificationDto(
            true,
            employeeName,
            programName,
            workspaceName,
            cert.IssuedAtUtc,
            cert.Code);
    }

    private async Task<(string EmployeeName, string ProgramName, string? ProgramDescription, string WorkspaceName)>
        ResolveCertificateContextAsync(TrainingCertificate cert, CancellationToken cancellationToken)
    {
        var user = await _authRepository.GetUserByIdAsync(cert.UserId, cancellationToken);
        var workspace = await _workspaceRepository.GetByIdAsync(cert.WorkspaceId, cancellationToken);
        var program = await _trainingRepository.GetProgramByIdAsync(cert.ProgramId, cert.WorkspaceId, cancellationToken);

        return (
            user?.FullName ?? "Funcionário",
            program?.Name ?? "Programa de treinamento",
            program?.Description,
            workspace?.Name ?? "Workspace");
    }

    private string BuildVerificationUrl(string code)
    {
        var baseUrl = (_options.PublicVerificationBaseUrl ?? string.Empty).TrimEnd('/');
        return $"{baseUrl}/{code}";
    }

    private static string MakeSafeFileName(string programName, string code)
    {
        var safe = new string(programName
            .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_')
            .ToArray())
            .Trim()
            .Replace(' ', '-');
        if (string.IsNullOrEmpty(safe)) safe = "certificado";
        return $"{safe}-{code}.pdf";
    }
}
