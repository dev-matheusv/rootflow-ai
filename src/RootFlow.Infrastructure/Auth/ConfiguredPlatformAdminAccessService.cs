using System.Net.Mail;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.PlatformAdmin;

namespace RootFlow.Infrastructure.Auth;

public sealed class ConfiguredPlatformAdminAccessService : IPlatformAdminAccessService
{
    private readonly HashSet<string> _normalizedEmails;

    public ConfiguredPlatformAdminAccessService(IOptions<PlatformAdminOptions> options)
    {
        _normalizedEmails = options.Value.Emails
            .Select(NormalizeEmail)
            .Where(static email => !string.IsNullOrWhiteSpace(email))
            .ToHashSet(StringComparer.Ordinal);
    }

    public bool HasAccess(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        return _normalizedEmails.Contains(NormalizeEmail(email));
    }

    private static string NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        try
        {
            return new MailAddress(email.Trim()).Address.ToUpperInvariant();
        }
        catch (FormatException)
        {
            return email.Trim().ToUpperInvariant();
        }
    }
}
