namespace RootFlow.Application.Abstractions.Documents;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(FileUpload file, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored file. Missing files are ignored; paths outside the storage root are refused.</summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
