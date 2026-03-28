namespace RootFlow.Application.Abstractions.Documents;

public sealed class FileUpload
{
    public FileUpload(string fileName, string contentType, long sizeBytes, Stream content)
    {
        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "File size cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(content);

        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        Content = content;
    }

    public string FileName { get; }

    public string ContentType { get; }

    public long SizeBytes { get; }

    public Stream Content { get; }
}
