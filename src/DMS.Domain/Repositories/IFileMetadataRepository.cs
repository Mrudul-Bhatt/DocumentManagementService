using DMS.Domain.Entities;

namespace DMS.Domain.Repositories;

public interface IFileMetadataRepository
{
    Task<FileMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FileMetadata>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(FileMetadata file, CancellationToken ct = default);
    Task DeleteAsync(FileMetadata file, CancellationToken ct = default);
}
