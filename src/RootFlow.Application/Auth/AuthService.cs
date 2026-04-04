using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing;
using RootFlow.Application.Auth.Commands;
using RootFlow.Application.Auth.Dtos;
using RootFlow.Domain.Users;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Auth;

public sealed class AuthService
{
    private static readonly Regex NonAlphaNumericRegex = new("[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);
    private const string PasswordResetRequestedMessage = "If the account exists, a reset link has been sent.";
    private const string InvalidPasswordResetTokenMessage = "This reset link is invalid or has expired. Request a new one.";
    private readonly IAuthRepository _authRepository;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IPasswordResetNotifier _passwordResetNotifier;
    private readonly IClock _clock;
    private readonly WorkspaceBillingService? _workspaceBillingService;

    public AuthService(
        IAuthRepository authRepository,
        IPasswordHashingService passwordHashingService,
        IPasswordResetNotifier passwordResetNotifier,
        IClock clock,
        WorkspaceBillingService? workspaceBillingService = null)
    {
        _authRepository = authRepository;
        _passwordHashingService = passwordHashingService;
        _passwordResetNotifier = passwordResetNotifier;
        _clock = clock;
        _workspaceBillingService = workspaceBillingService;
    }

    public async Task<AuthSessionDto> SignupAsync(
        SignupCommand command,
        CancellationToken cancellationToken = default)
    {
        var fullName = NormalizeRequiredValue(command.FullName, "Full name", maxLength: 120);
        var email = NormalizeEmail(command.Email);
        var workspaceName = NormalizeRequiredValue(command.WorkspaceName, "Workspace name", maxLength: 120);
        ValidatePassword(command.Password);

        var normalizedEmail = email.ToUpperInvariant();
        var existingUser = await _authRepository.GetUserByNormalizedEmailAsync(normalizedEmail, cancellationToken);
        if (existingUser is not null)
        {
            throw new AuthConflictException("An account with this email already exists.");
        }

        var createdAtUtc = _clock.UtcNow;
        var workspaceSlug = await GenerateUniqueWorkspaceSlugAsync(workspaceName, cancellationToken);

        var user = new AppUser(
            Guid.NewGuid(),
            email,
            fullName,
            _passwordHashingService.HashPassword(command.Password),
            createdAtUtc);

        // Signup always provisions a new workspace; shared workspaces will join through explicit invites later.
        var workspace = new Workspace(
            Guid.NewGuid(),
            workspaceName,
            workspaceSlug,
            createdAtUtc);

        var membership = new WorkspaceMembership(
            Guid.NewGuid(),
            workspace.Id,
            user.Id,
            WorkspaceRole.Owner,
            createdAtUtc);

        await _authRepository.CreateUserWorkspaceAsync(user, workspace, membership, cancellationToken);
        if (_workspaceBillingService is not null)
        {
            await _workspaceBillingService.EnsureTrialProvisionedAsync(workspace.Id, cancellationToken);
        }

        return MapSession(user, workspace, membership.Role);
    }

    public async Task<AuthSessionDto> LoginAsync(
        LoginCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(command.Email);
        if (string.IsNullOrWhiteSpace(command.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var user = await _authRepository.GetUserByNormalizedEmailAsync(email.ToUpperInvariant(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (!_passwordHashingService.VerifyPassword(user.PasswordHash, command.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var session = await _authRepository.GetPrimarySessionAsync(user.Id, cancellationToken);
        if (session is null)
        {
            throw new UnauthorizedAccessException("No active workspace membership was found for this account.");
        }

        return session;
    }

    public Task<AuthSessionDto?> GetCurrentSessionAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        return _authRepository.GetSessionAsync(userId, workspaceId, cancellationToken);
    }

    public async Task<string> RequestPasswordResetAsync(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(command.Email);
        var user = await _authRepository.GetUserByNormalizedEmailAsync(email.ToUpperInvariant(), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return PasswordResetRequestedMessage;
        }

        var createdAtUtc = _clock.UtcNow;
        var rawToken = GeneratePasswordResetToken();
        var passwordResetToken = new PasswordResetToken(
            Guid.NewGuid(),
            user.Id,
            HashPasswordResetToken(rawToken),
            createdAtUtc,
            createdAtUtc.Add(PasswordResetTokenLifetime));

        await _authRepository.StorePasswordResetTokenAsync(passwordResetToken, cancellationToken);
        await _passwordResetNotifier.SendResetLinkAsync(
            new PasswordResetNotification(
                user.Email,
                user.FullName,
                rawToken,
                passwordResetToken.ExpiresAtUtc),
            cancellationToken);

        return PasswordResetRequestedMessage;
    }

    public async Task ResetPasswordAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            throw new ArgumentException("Reset token is required.", nameof(command.Token));
        }

        ValidatePassword(command.NewPassword);

        var passwordResetToken = await _authRepository.GetPasswordResetTokenByHashAsync(
            HashPasswordResetToken(command.Token),
            cancellationToken);

        var utcNow = _clock.UtcNow;
        if (passwordResetToken is null || !passwordResetToken.IsActiveAt(utcNow))
        {
            throw new InvalidPasswordResetTokenException(InvalidPasswordResetTokenMessage);
        }

        var user = await _authRepository.GetUserByIdAsync(passwordResetToken.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new InvalidPasswordResetTokenException(InvalidPasswordResetTokenMessage);
        }

        await _authRepository.CompletePasswordResetAsync(
            user.Id,
            passwordResetToken.Id,
            _passwordHashingService.HashPassword(command.NewPassword),
            utcNow,
            cancellationToken);
    }

    private async Task<string> GenerateUniqueWorkspaceSlugAsync(
        string workspaceName,
        CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(workspaceName);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "workspace";
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await _authRepository.WorkspaceSlugExistsAsync(candidate, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static AuthSessionDto MapSession(AppUser user, Workspace workspace, WorkspaceRole role)
    {
        return new AuthSessionDto(
            new AuthUserDto(user.Id, user.FullName, user.Email),
            new AuthWorkspaceDto(workspace.Id, workspace.Name, workspace.Slug),
            role);
    }

    private static string NormalizeRequiredValue(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{fieldName} must be {maxLength} characters or less.", fieldName);
        }

        return trimmed;
    }

    private static string NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var trimmed = email.Trim();

        try
        {
            var address = new MailAddress(trimmed);
            return address.Address;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Email is invalid.", nameof(email), exception);
        }
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        if (password.Length < 8)
        {
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));
        }

        if (password.Length > 128)
        {
            throw new ArgumentException("Password must be 128 characters or less.", nameof(password));
        }
    }

    private static string GeneratePasswordResetToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashPasswordResetToken(string token)
    {
        var normalizedToken = token.Trim();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedToken));
        return Convert.ToHexString(hashBytes);
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var ascii = builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Trim()
            .ToLowerInvariant();

        var slug = NonAlphaNumericRegex.Replace(ascii, "-").Trim('-');
        return slug.Length <= 80 ? slug : slug[..80].Trim('-');
    }
}
