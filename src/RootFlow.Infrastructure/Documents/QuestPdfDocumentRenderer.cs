using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RootFlow.Application.Abstractions.Documents;

namespace RootFlow.Infrastructure.Documents;

public sealed partial class QuestPdfDocumentRenderer : IDocumentRenderer
{
    public Task<byte[]> RenderAsync(DocumentRenderRequest request, CancellationToken cancellationToken = default)
    {
        var resolvedBody = ResolveBody(request.TemplateBody, request.FieldValues);
        var paragraphs = resolvedBody
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.None)
            .ToList();

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2.4f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10.5f).FontFamily("Arial"));

                page.Header().Element(header =>
                {
                    header.Row(row =>
                    {
                        row.RelativeItem().Text("RootFlow")
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1)
                            .Bold()
                            .LetterSpacing(0.12f);

                        row.RelativeItem().AlignRight().Text(DateTime.UtcNow.ToString("dd/MM/yyyy"))
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().PaddingTop(0.6f, Unit.Centimetre).Column(col =>
                {
                    col.Item()
                        .PaddingBottom(0.6f, Unit.Centimetre)
                        .Text(request.TemplateName)
                        .FontSize(17)
                        .Bold()
                        .FontColor(Colors.Grey.Darken3);

                    col.Item()
                        .PaddingBottom(0.5f, Unit.Centimetre)
                        .LineHorizontal(0.5f)
                        .LineColor(Colors.Grey.Lighten2);

                    foreach (var paragraph in paragraphs)
                    {
                        if (string.IsNullOrWhiteSpace(paragraph))
                        {
                            col.Item().PaddingBottom(0.35f, Unit.Centimetre);
                            continue;
                        }

                        col.Item()
                            .PaddingBottom(0.3f, Unit.Centimetre)
                            .Text(paragraph)
                            .FontSize(10.5f)
                            .LineHeight(1.55f)
                            .FontColor(Colors.Grey.Darken3);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Gerado via RootFlow · ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                    text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Medium);
                    text.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();

        return Task.FromResult(pdfBytes);
    }

    private static string ResolveBody(string body, IReadOnlyDictionary<string, string> fieldValues)
    {
        return PlaceholderRegex().Replace(body, match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (!fieldValues.TryGetValue(key, out var value)) return match.Value;
            return DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date)
                ? date.ToString("dd/MM/yyyy")
                : value;
        });
    }

    [GeneratedRegex(@"\{\{([^}]+)\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}
