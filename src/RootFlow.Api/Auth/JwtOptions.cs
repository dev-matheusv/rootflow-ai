namespace RootFlow.Api.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "RootFlow";

    public string Audience { get; set; } = "RootFlow.Web";

    public string Key { get; set; } = string.Empty;

    public int ExpiresInMinutes { get; set; } = 480;
}
