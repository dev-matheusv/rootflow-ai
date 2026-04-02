namespace RootFlow.Infrastructure.Email;

public sealed record EmailMessage(
    string ToAddress,
    string? ToDisplayName,
    string Subject,
    string PlainTextBody,
    string HtmlBody);
