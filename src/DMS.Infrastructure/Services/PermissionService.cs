using DMS.Domain.Entities;
using DMS.Domain.Enums;
using DMS.Domain.Services;
using DMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace DMS.Infrastructure.Services;

/// <summary>
/// Resolves read/write/comment permissions for a user on a resource.
///
/// Cache strategy:
///   Each lookup key is "perm:{userId}:{resourceType}:{resourceId}".
///   A hit returns the cached ShareRole as a single byte; a miss walks the DB.
///   All keys for a resource are invalidated on any share mutation by calling
///   InvalidateCacheAsync, which removes every grantee's cached entry for that resource.
///
///   If Redis is unavailable, StackExchange.Redis throws RedisConnectionException.
///   We catch it and fall through to the DB so that a Redis outage does not take
///   down the entire service — correctness is maintained at the cost of latency.
///
/// Inheritance rule:
///   Folder shares are inherited by all descendants. When checking a file or subfolder,
///   we walk ancestor folders looking for an explicit share. The first match wins
///   (child overrides parent). This is implemented via a recursive CTE on Folders.
/// </summary>
internal sealed class PermissionService(
    AppDbContext db,
    IDistributedCache cache,
    ILogger<PermissionService> logger) : IPermissionService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public Task<bool> CanReadAsync(Guid userId, Guid resourceId, ShareResourceType resourceType, CancellationToken ct) =>
        HasRoleAtLeastAsync(userId, resourceId, resourceType, ShareRole.Viewer, ct);

    public Task<bool> CanWriteAsync(Guid userId, Guid resourceId, ShareResourceType resourceType, CancellationToken ct) =>
        HasRoleAtLeastAsync(userId, resourceId, resourceType, ShareRole.Editor, ct);

    public Task<bool> CanCommentAsync(Guid userId, Guid resourceId, ShareResourceType resourceType, CancellationToken ct) =>
        HasRoleAtLeastAsync(userId, resourceId, resourceType, ShareRole.Commenter, ct);

    public async Task InvalidateCacheAsync(Guid resourceId, ShareResourceType resourceType, CancellationToken ct)
    {
        // Remove cached entries for every grantee of this resource.
        var granteeIds = await db.Shares
            .Where(s => s.ResourceId == resourceId)
            .Select(s => s.GrantedToUserId)
            .ToListAsync(ct);

        foreach (var granteeId in granteeIds)
        {
            var key = CacheKey(granteeId, resourceId, resourceType);
            try { await cache.RemoveAsync(key, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Redis remove failed for key {Key}", key); }
        }
    }

    private async Task<bool> HasRoleAtLeastAsync(
        Guid userId,
        Guid resourceId,
        ShareResourceType resourceType,
        ShareRole required,
        CancellationToken ct)
    {
        var role = await GetEffectiveRoleAsync(userId, resourceId, resourceType, ct);
        return role.HasValue && role.Value >= required;
    }

    private async Task<ShareRole?> GetEffectiveRoleAsync(
        Guid userId,
        Guid resourceId,
        ShareResourceType resourceType,
        CancellationToken ct)
    {
        var key = CacheKey(userId, resourceId, resourceType);

        // Try cache first.
        try
        {
            var cached = await cache.GetAsync(key, ct);
            if (cached is { Length: 1 })
                return (ShareRole)cached[0];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis get failed for key {Key} — falling through to DB", key);
        }

        // Check direct share on the resource.
        var direct = await db.Shares
            .FirstOrDefaultAsync(s => s.ResourceId == resourceId && s.ResourceType == resourceType && s.GrantedToUserId == userId, ct);

        if (direct is not null)
        {
            await TryCacheRoleAsync(key, direct.Role, ct);
            return direct.Role;
        }

        // For files: check the containing folder ancestry.
        // For folders: check parent folder ancestry.
        var inherited = await GetInheritedRoleAsync(userId, resourceId, resourceType, ct);
        if (inherited.HasValue)
            await TryCacheRoleAsync(key, inherited.Value, ct);

        return inherited;
    }

    private async Task<ShareRole?> GetInheritedRoleAsync(
        Guid userId,
        Guid resourceId,
        ShareResourceType resourceType,
        CancellationToken ct)
    {
        IReadOnlyList<Guid> ancestorFolderIds;

        if (resourceType == ShareResourceType.File)
        {
            // Walk: file → its folder → parent folders up to root.
            var file = await db.FileMetadata
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == resourceId, ct);

            if (file?.FolderId is null)
                return null;

            ancestorFolderIds = await GetAncestorFolderIdsAsync(file.FolderId.Value, ct);
        }
        else
        {
            // Folder — check parent folders.
            var folder = await db.Folders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == resourceId, ct);

            if (folder?.ParentFolderId is null)
                return null;

            ancestorFolderIds = await GetAncestorFolderIdsAsync(folder.ParentFolderId.Value, ct);
        }

        if (ancestorFolderIds.Count == 0)
            return null;

        // Find the closest ancestor that has an explicit share for this user.
        // ancestorFolderIds is ordered from nearest to farthest — first match wins.
        foreach (var ancestorId in ancestorFolderIds)
        {
            var share = await db.Shares
                .FirstOrDefaultAsync(
                    s => s.ResourceId == ancestorId &&
                         s.ResourceType == ShareResourceType.Folder &&
                         s.GrantedToUserId == userId,
                    ct);

            if (share is not null)
                return share.Role;
        }

        return null;
    }

    /// <summary>
    /// Returns ancestor folder IDs in order from nearest (self or direct parent) to root,
    /// using a recursive CTE that walks up the ParentFolderId chain.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> GetAncestorFolderIdsAsync(Guid startFolderId, CancellationToken ct)
    {
        var ids = await db.Database
            .SqlQueryRaw<Guid>(
                """
                WITH FolderAncestors AS (
                    SELECT Id, ParentFolderId FROM Folders WHERE Id = {0}
                    UNION ALL
                    SELECT f.Id, f.ParentFolderId FROM Folders f
                    INNER JOIN FolderAncestors fa ON f.Id = fa.ParentFolderId
                )
                SELECT Id FROM FolderAncestors
                """,
                startFolderId)
            .ToListAsync(ct);

        return ids.AsReadOnly();
    }

    private async Task TryCacheRoleAsync(string key, ShareRole role, CancellationToken ct)
    {
        try
        {
            await cache.SetAsync(
                key,
                [(byte)role],
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis set failed for key {Key}", key);
        }
    }

    private static string CacheKey(Guid userId, Guid resourceId, ShareResourceType resourceType) =>
        $"perm:{userId}:{resourceType}:{resourceId}";
}
