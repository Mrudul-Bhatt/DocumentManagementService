namespace DMS.Domain.Services;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream content, string userId, string fileId, CancellationToken ct = default);
    Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
