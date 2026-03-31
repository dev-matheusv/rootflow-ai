using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Auth.Dtos;

namespace RootFlow.Api.Auth;

public sealed class JwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenGenerator(
        IOptions<JwtOptions> options,
        IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public GeneratedJwtToken Generate(AuthSessionDto session)
    {
        var now = _clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(_options.ExpiresInMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, session.User.Id.ToString()),
            new(ClaimTypes.NameIdentifier, session.User.Id.ToString()),
            new(ClaimTypes.Name, session.User.FullName),
            new(ClaimTypes.Email, session.User.Email),
            new(ClaimTypes.Role, session.Role.ToString()),
            new(RootFlowClaimTypes.WorkspaceId, session.Workspace.Id.ToString()),
            new(RootFlowClaimTypes.WorkspaceSlug, session.Workspace.Slug),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var descriptor = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(CreateSigningKey(_options.Key), SecurityAlgorithms.HmacSha256));

        return new GeneratedJwtToken(
            new JwtSecurityTokenHandler().WriteToken(descriptor),
            expiresAtUtc);
    }

    public static SymmetricSecurityKey CreateSigningKey(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            throw new InvalidOperationException("JWT signing key must be configured with at least 32 characters.");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
    }
}

public sealed record GeneratedJwtToken(
    string Token,
    DateTime ExpiresAtUtc);
