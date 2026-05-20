using DMS.Domain.Enums;

namespace DMS.Domain.Services;

/// <summary>
/// Resolves the effective permission a user has on a file or folder.
///
/// Defined in the Domain layer so that Application handlers can depend on it
/// without taking a reference to Infrastructure. The concrete implementation
/// (with Redis caching and EF queries) lives in DMS.Infrastructure.
///
/// Resolution order:
///   1. User is the resource owner → all operations allowed, no DB query needed.
///   2. Explicit Share row for (resourceId, userId) → use that role.
///   3. Walk folder ancestry upward; the first Share found is the inherited role.
///   4. No share found → access denied.
///
/// Resolved results are cached in Redis with a short TTL to keep p99 latency
/// under the 20 ms target without hammering the database on every request.
/// </summary>
public interface IPermissionService
{
    /// <summary>Returns true if the user can read (download, list) the resource.</summary>
    Task<bool> CanReadAsync(Guid userId, Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default);

    /// <summary>Returns true if the user can write (upload, rename, move, delete) the resource.</summary>
    Task<bool> CanWriteAsync(Guid userId, Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default);

    /// <summary>Returns true if the user can add comments. Requires Commenter or Editor role.</summary>
    Task<bool> CanCommentAsync(Guid userId, Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default);

    /// <summary>
    /// Invalidates any cached permission entries for this resource.
    /// Called after every share creation or revocation so that the next
    /// permission check reflects the updated ACL immediately.
    /// </summary>
    Task InvalidateCacheAsync(Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default);
}
