using Microsoft.AspNetCore.Identity;
using RootFlow.Application.Abstractions.Auth;

namespace RootFlow.Infrastructure.Auth;

public sealed class AspNetPasswordHashingService : IPasswordHashingService
{
    private readonly PasswordHasher<PasswordHashSubject> _passwordHasher = new();
    private readonly PasswordHashSubject _subject = new();

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _passwordHasher.HashPassword(_subject, password);
    }

    public bool VerifyPassword(string passwordHash, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        return _passwordHasher.VerifyHashedPassword(_subject, passwordHash, providedPassword) != PasswordVerificationResult.Failed;
    }

    private sealed class PasswordHashSubject;
}
