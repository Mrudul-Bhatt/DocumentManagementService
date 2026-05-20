using DMS.Domain.Entities;
using DMS.Domain.Enums;

namespace DMS.Domain.Repositories;

public interface IShareRepository
{
    Task<Share?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns all shares on a resource, ordered by creation date descending.</summary>
    Task<IReadOnlyList<Share>> GetByResourceAsync(Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default);

    /// <summary>Returns all resources shared with a specific user.</summary>
    Task<IReadOnlyList<Share>> GetForUserAsync(Guid grantedToUserId, CancellationToken ct = default);

    /// <summary>
    /// Returns the single share for a (resource, grantee) pair, or null if none exists.
    /// Used by CreateShareCommandHandler to detect duplicate grants and by
    /// PermissionService to find a direct share for the requesting user.
    /// </summary>
    Task<Share?> GetDirectShareAsync(Guid resourceId, ShareResourceType resourceType, Guid grantedToUserId, CancellationToken ct = default);

    /// <summary>Returns the total number of shares on a resource. Used to enforce the 500-principal cap.</summary>
    Task<int> CountByResourceAsync(Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default);

    Task AddAsync(Share share, CancellationToken ct = default);
    Task DeleteAsync(Share share, CancellationToken ct = default);
}
