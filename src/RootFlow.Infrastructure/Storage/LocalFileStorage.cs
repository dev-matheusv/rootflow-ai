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

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return Task.CompletedTask;
        }

        // Only delete inside the storage root: a tampered path must never remove other files.
        var rootPath = Path.GetFullPath(ResolveRootPath(_options.RootPath));
        var fullPath = Path.GetFullPath(storagePath);
        var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar) ? rootPath : rootPath + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to delete a file outside the storage root.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
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
