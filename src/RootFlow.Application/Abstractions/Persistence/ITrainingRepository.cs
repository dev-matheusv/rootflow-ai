using RootFlow.Domain.Training;

namespace RootFlow.Application.Abstractions.Persistence;

public interface ITrainingRepository
{
    // Programs
    Task AddProgramAsync(TrainingProgram program, CancellationToken cancellationToken = default);
    Task UpdateProgramAsync(TrainingProgram program, CancellationToken cancellationToken = default);
    Task<TrainingProgram?> GetProgramByIdAsync(Guid programId, Guid workspaceId, CancellationToken cancellationToken = default);
    Task<TrainingProgram?> GetProgramBySlugAsync(string slug, Guid workspaceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingProgram>> ListProgramsByWorkspaceAsync(
        Guid workspaceId,
        bool publishedOnly,
        CancellationToken cancellationToken = default);
    Task<bool> ProgramSlugExistsAsync(string slug, Guid workspaceId, CancellationToken cancellationToken = default);

    // Modules
    Task AddModuleAsync(TrainingModule module, CancellationToken cancellationToken = default);
    Task UpdateModuleAsync(TrainingModule module, CancellationToken cancellationToken = default);
    Task DeleteModuleAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<TrainingModule?> GetModuleByIdAsync(Guid moduleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingModule>> ListModulesByProgramAsync(Guid programId, CancellationToken cancellationToken = default);

    // Questions
    Task AddQuestionAsync(TrainingQuestion question, CancellationToken cancellationToken = default);
    Task AddQuestionsBulkAsync(IEnumerable<TrainingQuestion> questions, CancellationToken cancellationToken = default);
    Task UpdateQuestionAsync(TrainingQuestion question, CancellationToken cancellationToken = default);
    Task DeleteQuestionAsync(Guid questionId, CancellationToken cancellationToken = default);
    Task<TrainingQuestion?> GetQuestionByIdAsync(Guid questionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingQuestion>> ListQuestionsByModuleAsync(
        Guid moduleId,
        bool publishedOnly,
        CancellationToken cancellationToken = default);

    // Attempts
    Task AddAttemptAsync(TrainingAttempt attempt, CancellationToken cancellationToken = default);
    Task UpdateAttemptAsync(TrainingAttempt attempt, CancellationToken cancellationToken = default);
    Task<TrainingAttempt?> GetAttemptByIdAsync(Guid attemptId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingAttempt>> ListAttemptsByUserAndModuleAsync(
        Guid userId,
        Guid moduleId,
        CancellationToken cancellationToken = default);

    // Answers
    Task AddAnswersAsync(IEnumerable<TrainingAnswer> answers, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingAnswer>> ListAnswersByAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default);

    // Certificates
    Task AddCertificateAsync(TrainingCertificate certificate, CancellationToken cancellationToken = default);
    Task<TrainingCertificate?> GetCertificateByIdAsync(Guid certificateId, Guid workspaceId, CancellationToken cancellationToken = default);
    Task<TrainingCertificate?> GetCertificateByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<TrainingCertificate?> GetCertificateByProgramAndUserAsync(Guid programId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingCertificate>> ListCertificatesByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
