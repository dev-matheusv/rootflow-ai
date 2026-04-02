using System.Text;
using System.Text.Encodings.Web;

namespace RootFlow.Infrastructure.Email;

internal sealed record ActionEmailTemplate(
    string Subject,
    string Preheader,
    string Eyebrow,
    string Heading,
    string Intro,
    string ActionText,
    string ActionUrl,
    IReadOnlyList<string> DetailLines,
    string SupportingText,
    string Footer);

internal static class RootFlowEmailTemplate
{
    public static EmailMessage CreateMessage(
        string toAddress,
        string? toDisplayName,
        ActionEmailTemplate template)
    {
        return new EmailMessage(
            toAddress,
            toDisplayName,
            template.Subject,
            BuildPlainText(template),
            BuildHtml(template));
    }

    private static string BuildPlainText(ActionEmailTemplate template)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RootFlow");
        builder.AppendLine();
        builder.AppendLine(template.Heading);
        builder.AppendLine();
        builder.AppendLine(template.Intro);
        builder.AppendLine();

        foreach (var detailLine in template.DetailLines.Where(detailLine => !string.IsNullOrWhiteSpace(detailLine)))
        {
            builder.AppendLine($"- {detailLine}");
        }

        builder.AppendLine();
        builder.AppendLine($"{template.ActionText}: {template.ActionUrl}");
        builder.AppendLine();
        builder.AppendLine(template.SupportingText);
        builder.AppendLine();
        builder.AppendLine(template.Footer);

        return builder.ToString().Trim();
    }

    private static string BuildHtml(ActionEmailTemplate template)
    {
        var preheader = Encode(template.Preheader);
        var eyebrow = Encode(template.Eyebrow);
        var heading = Encode(template.Heading);
        var intro = Encode(template.Intro);
        var actionText = Encode(template.ActionText);
        var actionUrl = Encode(template.ActionUrl);
        var supportingText = Encode(template.SupportingText);
        var footer = Encode(template.Footer);
        var detailLines = string.Join(
            string.Empty,
            template.DetailLines
                .Where(detailLine => !string.IsNullOrWhiteSpace(detailLine))
                .Select(detailLine =>
                    $"""<li style="margin:0 0 8px; color:#475569;">{Encode(detailLine)}</li>"""));

        var detailsSection = string.IsNullOrWhiteSpace(detailLines)
            ? string.Empty
            : $"""
               <ul style="margin:0 0 24px; padding-left:20px; font-size:15px; line-height:1.7;">
                 {detailLines}
               </ul>
               """;

        return $"""
                <!doctype html>
                <html lang="en">
                  <body style="margin:0; padding:0; background:#f4f7fb; color:#0f172a; font-family:Arial,Helvetica,sans-serif;">
                    <span style="display:none!important; visibility:hidden; opacity:0; color:transparent; height:0; width:0; overflow:hidden;">
                      {preheader}
                    </span>
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f4f7fb; padding:32px 16px;">
                      <tr>
                        <td align="center">
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px; background:#ffffff; border:1px solid #dbe4f0; border-radius:24px; overflow:hidden; box-shadow:0 20px 50px rgba(15,35,70,0.08);">
                            <tr>
                              <td style="padding:32px 36px 12px;">
                                <div style="font-size:12px; line-height:1.4; letter-spacing:0.18em; text-transform:uppercase; font-weight:700; color:#0f63ec; margin-bottom:16px;">
                                  {eyebrow}
                                </div>
                                <h1 style="margin:0 0 14px; font-size:30px; line-height:1.18; color:#0f172a;">
                                  {heading}
                                </h1>
                                <p style="margin:0 0 24px; font-size:15px; line-height:1.7; color:#475569;">
                                  {intro}
                                </p>
                                {detailsSection}
                                <div style="margin:0 0 24px;">
                                  <a href="{actionUrl}" style="display:inline-block; padding:14px 22px; border-radius:14px; background:#0f63ec; color:#ffffff; text-decoration:none; font-size:15px; font-weight:700;">
                                    {actionText}
                                  </a>
                                </div>
                                <p style="margin:0 0 14px; font-size:14px; line-height:1.7; color:#64748b;">
                                  {supportingText}
                                </p>
                                <p style="margin:0; font-size:13px; line-height:1.7; word-break:break-all;">
                                  <a href="{actionUrl}" style="color:#0f63ec; text-decoration:none;">{actionUrl}</a>
                                </p>
                              </td>
                            </tr>
                            <tr>
                              <td style="padding:20px 36px 32px; border-top:1px solid #e2e8f0; font-size:12px; line-height:1.7; color:#64748b;">
                                {footer}
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>
                  </body>
                </html>
                """;
    }

    private static string Encode(string value)
    {
        return HtmlEncoder.Default.Encode(value);
    }
}
