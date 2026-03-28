using System.Text;
using RootFlow.Application.Abstractions.Documents;
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

    public async Task<string> ExtractTextAsync(StoredFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (extension == ".pdf")
        {
            var pdfText = await Task.Run(() => ExtractPdfText(file.StoragePath), cancellationToken);
            return NormalizeText(pdfText);
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
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string NormalizeText(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }
}
