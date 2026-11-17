using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;

namespace RootFlow.Application.Account;

public sealed class AccountDeletionBlockedException : Exception
{
    public AccountDeletionBlockedException(AccountDeletionBlockers blockers)
        : base(blockers.SharedOwnedWorkspaces.Count > 0
            ? "You own a workspace that other people use. Transfer ownership or remove the other members before deleting your account."
            : "A workspace you own still has an active paid subscription. Cancel it in the billing portal before deleting your account.")
    {
        Blockers = blockers;
    }

    public AccountDeletionBlockers Blockers { get; }
}

/// <summary>RF-13: data subject rights (LGPD art. 18): export my data and delete my account.</summary>
public sealed class AccountService
{
    private readonly IAuthRepository _authRepository;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IPersonalDataRepository _personalDataRepository;
    private readonly IClock _clock;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        IAuthRepository authRepository,
        IPasswordHashingService passwordHashingService,
        IPersonalDataRepository personalDataRepository,
        IClock clock,
        IFileStorage fileStorage,
        ILogger<AccountService> logger)
    {
        _fileStorage = fileStorage;
        _logger = logger;
        _authRepository = authRepository;
        _passwordHashingService = passwordHashingService;
        _personalDataRepository = personalDataRepository;
        _clock = clock;
    }

    public Task<PersonalDataExport?> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
        => _personalDataRepository.ExportAsync(userId, _clock.UtcNow, cancellationToken);

    public async Task DeleteAccountAsync(Guid userId, string? password, CancellationToken cancellationToken = default)
    {
        // Re-authenticate: a stolen session alone must not be enough to erase an account.
        var user = await _authRepository.GetUserByIdAsync(userId, cancellationToken);
        if (user is null
            || string.IsNullOrEmpty(password)
            || !_passwordHashingService.VerifyPassword(user.PasswordHash, password))
        {
            throw new UnauthorizedAccessException("Password is incorrect.");
        }

        var blockers = await _personalDataRepository.GetDeletionBlockersAsync(userId, _clock.UtcNow, cancellationToken);
        if (blockers.Any)
        {
            throw new AccountDeletionBlockedException(blockers);
        }

        var storagePaths = await _personalDataRepository.DeleteUserAsync(userId, cancellationToken);

        // Files go after the commit: the data is already gone from search; a failed file delete only
        // leaves an orphan file, which is logged (path is internal, no personal data in the log).
        foreach (var path in storagePaths)
        {
            try
            {
                await _fileStorage.DeleteAsync(path, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Could not delete a stored file after account deletion. UserId={UserId}", userId);
            }
        }
    }
}
