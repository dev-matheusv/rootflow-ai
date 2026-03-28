namespace RootFlow.Application.Abstractions.Documents;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(FileUpload file, CancellationToken cancellationToken = default);
}
