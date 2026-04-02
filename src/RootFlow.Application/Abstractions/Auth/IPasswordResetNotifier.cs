namespace RootFlow.Application.Abstractions.Auth;

public interface IPasswordResetNotifier
{
    Task SendResetLinkAsync(
        PasswordResetNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record PasswordResetNotification(
    string Email,
    string FullName,
    string Token,
    DateTime ExpiresAtUtc);
