using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

internal sealed class FolderRepository(AppDbContext dbContext) : IFolderRepository
{
    public async Task<Folder?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Folders.FindAsync([id], ct);

    public async Task<IReadOnlyList<Folder>> GetRootFoldersAsync(Guid ownerId, CancellationToken ct = default) =>
        await dbContext.Folders
            .Where(f => f.OwnerId == ownerId && f.ParentFolderId == null)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Folder>> GetChildrenAsync(Guid parentFolderId, Guid ownerId, CancellationToken ct = default) =>
        await dbContext.Folders
            .Where(f => f.ParentFolderId == parentFolderId && f.OwnerId == ownerId)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

    /// <summary>
    /// Returns all descendant folder IDs using a recursive CTE.
    /// The anchor selects direct children; the recursive part walks down the tree.
    /// Does not include the root folder itself — callers append it where needed.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetDescendantIdsAsync(Guid folderId, CancellationToken ct = default)
    {
        // EF Core does not support recursive CTEs via LINQ — raw SQL is required.
        // IgnoreQueryFilters is not needed here: we want to traverse even soft-deleted
        // descendants so that the delete handler can find and soft-delete them all.
        var ids = await dbContext.Database
            .SqlQueryRaw<Guid>(
                """
                WITH FolderTree AS (
                    SELECT Id FROM Folders WHERE ParentFolderId = {0}
                    UNION ALL
                    SELECT f.Id FROM Folders f
                    INNER JOIN FolderTree ft ON f.ParentFolderId = ft.Id
                )
                SELECT Id FROM FolderTree
                """,
                folderId)
            .ToListAsync(ct);

        return ids.AsReadOnly();
    }

    /// <summary>
    /// Returns the nesting depth of the given folder (0 = root, 1 = child of root, …).
    /// Used to enforce the 20-level maximum before creating or moving a folder.
    /// </summary>
    public async Task<int> GetDepthAsync(Guid folderId, CancellationToken ct = default)
    {
        var depths = await dbContext.Database
            .SqlQueryRaw<int>(
                """
                WITH FolderAncestors AS (
                    SELECT Id, ParentFolderId, 0 AS Depth FROM Folders WHERE Id = {0}
                    UNION ALL
                    SELECT f.Id, f.ParentFolderId, fa.Depth + 1
                    FROM Folders f
                    INNER JOIN FolderAncestors fa ON f.Id = fa.ParentFolderId
                )
                SELECT MAX(Depth) FROM FolderAncestors
                """,
                folderId)
            .ToListAsync(ct);

        return depths.FirstOrDefault();
    }

    public async Task AddAsync(Folder folder, CancellationToken ct = default)
    {
        await dbContext.Folders.AddAsync(folder, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Folder folder, CancellationToken ct = default)
    {
        dbContext.Folders.Update(folder);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Folder folder, CancellationToken ct = default)
    {
        dbContext.Folders.Remove(folder);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Folder>> GetDeletedByUserIdAsync(Guid ownerId, CancellationToken ct = default) =>
        await dbContext.Folders
            .IgnoreQueryFilters()
            .Where(f => f.OwnerId == ownerId && f.DeletedAt != null)
            .OrderByDescending(f => f.DeletedAt)
            .ToListAsync(ct);

    public async Task<Folder?> GetDeletedByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Folders
            .IgnoreQueryFilters()
            .Where(f => f.Id == id && f.DeletedAt != null)
            .FirstOrDefaultAsync(ct);
}
