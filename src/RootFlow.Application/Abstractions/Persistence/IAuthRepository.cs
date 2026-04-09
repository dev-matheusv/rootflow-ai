using RootFlow.Application.Auth.Dtos;
using RootFlow.Domain.Users;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IAuthRepository
{
    Task<AppUser?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task<AppUser?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> WorkspaceSlugExistsAsync(
        string slug,
        CancellationToken cancellationToken = default);

    Task CreateUserAsync(
        AppUser user,
        CancellationToken cancellationToken = default);

    Task CreateUserWorkspaceAsync(
        AppUser user,
        Workspace workspace,
        WorkspaceMembership membership,
        CancellationToken cancellationToken = default);

    Task<AuthSessionDto?> GetPrimarySessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AuthSessionDto?> GetSessionAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task StorePasswordResetTokenAsync(
        PasswordResetToken passwordResetToken,
        CancellationToken cancellationToken = default);

    Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task CompletePasswordResetAsync(
        Guid userId,
        Guid passwordResetTokenId,
        string passwordHash,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default);
}
