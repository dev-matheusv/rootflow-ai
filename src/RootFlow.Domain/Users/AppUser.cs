using System.Net.Mail;

namespace RootFlow.Domain.Users;

public sealed class AppUser
{
    private AppUser()
    {
    }

    public AppUser(
        Guid id,
        string email,
        string fullName,
        string passwordHash,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        Email = NormalizeAndValidateEmail(email);
        NormalizedEmail = Email.ToUpperInvariant();
        FullName = fullName.Trim();
        PasswordHash = passwordHash;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
        Id = id;
    }

    public Guid Id { get; private set; }

    public string Email { get; private set; } = null!;

    public string NormalizedEmail { get; private set; } = null!;

    public string FullName { get; private set; } = null!;

    public string PasswordHash { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        FullName = fullName.Trim();
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string NormalizeAndValidateEmail(string email)
    {
        var trimmed = email.Trim();

        try
        {
            var address = new MailAddress(trimmed);
            return address.Address;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Email address is invalid.", nameof(email), exception);
        }
    }
}
