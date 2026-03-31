namespace RootFlow.Application.Abstractions.Auth;

public interface IPasswordHashingService
{
    string HashPassword(string password);

    bool VerifyPassword(string passwordHash, string providedPassword);
}
