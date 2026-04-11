using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Infrastructure.Configuration;
using UglyToad.PdfPig;

namespace RootFlow.Infrastructure.Documents;

public sealed class SimpleDocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> PlainTextExtensions =
    [
        ".txt",
        ".md",
        ".csv",
        ".json",
        ".xml",
        ".html",
        ".htm",
        ".log"
    ];

    private static readonly HashSet<string> ImageExtensions =
    [
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiOptions _openAiOptions;

    public SimpleDocumentTextExtractor(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiOptions> openAiOptions)
    {
        _httpClientFactory = httpClientFactory;
        _openAiOptions = openAiOptions.Value;
    }

    public async Task<string> ExtractTextAsync(StoredFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (extension == ".pdf")
        {
            var pdfText = await Task.Run(() => ExtractPdfText(file.StoragePath), cancellationToken);
            return NormalizeText(pdfText);
        }

        if (extension == ".docx")
        {
            var docxText = await Task.Run(() => ExtractDocxText(file.StoragePath), cancellationToken);
            return NormalizeText(docxText);
        }

        if (extension == ".pptx")
        {
            var pptxText = await Task.Run(() => ExtractPptxText(file.StoragePath), cancellationToken);
            return NormalizeText(pptxText);
        }

        if (extension == ".xlsx")
        {
            var xlsxText = await Task.Run(() => ExtractXlsxText(file.StoragePath), cancellationToken);
            return NormalizeText(xlsxText);
        }

        if (ImageExtensions.Contains(extension))
        {
            var imageText = await ExtractImageTextAsync(file.StoragePath, extension, cancellationToken);
            return NormalizeText(imageText);
        }

        if (PlainTextExtensions.Contains(extension))
        {
            var content = await File.ReadAllTextAsync(file.StoragePath, cancellationToken);
            return NormalizeText(content);
        }

        throw new InvalidOperationException($"The file type '{extension}' is not supported for text extraction.");
    }

    private static string ExtractPdfText(string path)
    {
        using var document = PdfDocument.Open(path);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            // Try page.Text first; fall back to word extraction if empty
            var text = page.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                var words = page.GetWords();
                text = string.Join(" ", words.Select(w => w.Text));
            }

            if (!string.IsNullOrWhiteSpace(text))
                builder.AppendLine(text);
        }

        return builder.ToString();
    }

    private static string ExtractDocxText(string path)
    {
        using var document = WordprocessingDocument.Open(path, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var builder = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = paragraph.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine(text);
            }
        }

        return builder.ToString();
    }

    private static string ExtractPptxText(string path)
    {
        using var presentation = PresentationDocument.Open(path, false);
        var presentationPart = presentation.PresentationPart;
        if (presentationPart is null) return string.Empty;

        var builder = new StringBuilder();
        var slideOrder = presentationPart.Presentation?.SlideIdList?
            .Descendants<SlideId>()
            .Select(s => s.RelationshipId?.Value)
            .Where(r => r is not null)
            .ToList() ?? [];

        foreach (var relationshipId in slideOrder)
        {
            if (relationshipId is null) continue;
            if (!presentationPart.TryGetPartById(relationshipId!, out var part)) continue;
            if (part is not SlidePart slidePart) continue;

            foreach (var text in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    builder.AppendLine(text.Text);
                }
            }
        }

        return builder.ToString();
    }

    private static string ExtractXlsxText(string path)
    {
        using var workbook = new XLWorkbook(path);
        var builder = new StringBuilder();

        foreach (var worksheet in workbook.Worksheets)
        {
            builder.AppendLine($"[Sheet: {worksheet.Name}]");
            foreach (var row in worksheet.RowsUsed())
            {
                var cells = row.CellsUsed()
                    .Select(c => c.GetFormattedString())
                    .Where(v => !string.IsNullOrWhiteSpace(v));

                var line = string.Join("\t", cells);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    builder.AppendLine(line);
                }
            }
        }

        return builder.ToString();
    }

    private async Task<string> ExtractImageTextAsync(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured. Image text extraction is unavailable.");
        }

        var imageBytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var base64 = Convert.ToBase64String(imageBytes);
        var mimeType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };

        var payload = new
        {
            model = "gpt-4o-mini",
            max_tokens = 2000,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Extract all text content from this image. Return only the extracted text, preserving the original structure as much as possible. If there is no text, return an empty string." },
                        new
                        {
                            type = "image_url",
                            image_url = new { url = $"data:{mimeType};base64,{base64}" }
                        }
                    }
                }
            }
        };

        var client = _httpClientFactory.CreateClient();
        var baseUrl = _openAiOptions.BaseUrl.TrimEnd('/');

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static string NormalizeText(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }
}
