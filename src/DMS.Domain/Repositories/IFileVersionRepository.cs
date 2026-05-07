using DMS.Domain.Entities;

namespace DMS.Domain.Repositories;

public interface IFileVersionRepository
{
    Task<FileVersion?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all versions for a file ordered by VersionNumber descending (newest first).</summary>
    Task<IReadOnlyList<FileVersion>> GetByFileIdAsync(Guid fileId, CancellationToken ct = default);

    /// <summary>
    /// Returns the highest VersionNumber for the given file, or 0 if no versions exist yet.
    /// Used by the upload handler to calculate the next version number.
    /// </summary>
    Task<int> GetLatestVersionNumberAsync(Guid fileId, CancellationToken ct = default);

    Task AddAsync(FileVersion version, CancellationToken ct = default);
    Task DeleteAsync(FileVersion version, CancellationToken ct = default);
}
