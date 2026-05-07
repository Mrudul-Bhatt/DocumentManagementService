using DMS.Domain.Entities;

namespace DMS.Domain.Repositories;

public interface IFolderRepository
{
    Task<Folder?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<Folder>> GetChildrenAsync(Guid parentFolderId, Guid ownerId, CancellationToken ct = default);

    /// <summary>
    /// Returns the IDs of all descendant folders under the given folder using a recursive CTE.
    /// Used to cascade soft-delete or hard-delete an entire subtree atomically.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid folderId, CancellationToken ct = default);

    Task AddAsync(Folder folder, CancellationToken ct = default);
    Task UpdateAsync(Folder folder, CancellationToken ct = default);

    /// <summary>Hard-deletes a folder row. Only called by the trash purge job, not the normal delete flow.</summary>
    Task DeleteAsync(Folder folder, CancellationToken ct = default);

    /// <summary>Returns all soft-deleted folders owned by the user (ignores the global DeletedAt filter).</summary>
    Task<IReadOnlyList<Folder>> GetDeletedByUserIdAsync(Guid ownerId, CancellationToken ct = default);

    /// <summary>Loads a single soft-deleted folder by ID (ignores the global filter). Returns null if not found or not deleted.</summary>
    Task<Folder?> GetDeletedByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the nesting depth of the given folder (root = 0, child of root = 1, etc.).</summary>
    Task<int> GetDepthAsync(Guid folderId, CancellationToken ct = default);
}
