using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly StorageOptions _options;
    private readonly IClock _clock;

    public LocalFileStorage(IOptions<StorageOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public async Task<StoredFile> SaveAsync(FileUpload file, CancellationToken cancellationToken = default)
    {
        var rootPath = ResolveRootPath(_options.RootPath);
        var utcNow = _clock.UtcNow;
        var folderPath = Path.Combine(
            rootPath,
            utcNow.ToString("yyyy"),
            utcNow.ToString("MM"),
            utcNow.ToString("dd"));

        Directory.CreateDirectory(folderPath);

        var extension = Path.GetExtension(file.FileName);
        var storedFileName = $"{utcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(folderPath, storedFileName);

        await using var targetStream = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);

        await file.Content.CopyToAsync(targetStream, cancellationToken);

        return new StoredFile(
            absolutePath,
            file.FileName,
            file.ContentType,
            file.SizeBytes);
    }

    private static string ResolveRootPath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));
    }
}
