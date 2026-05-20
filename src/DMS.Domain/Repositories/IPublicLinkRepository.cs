using DMS.Domain.Entities;
using DMS.Domain.Enums;

namespace DMS.Domain.Repositories;

public interface IPublicLinkRepository
{
    Task<PublicLink?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Looks up a public link by its token string. Backed by a unique index — O(log n).</summary>
    Task<PublicLink?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Returns all public links for a resource, ordered by creation date descending.</summary>
    Task<IReadOnlyList<PublicLink>> GetByResourceAsync(Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default);

    Task AddAsync(PublicLink link, CancellationToken ct = default);
    Task DeleteAsync(PublicLink link, CancellationToken ct = default);
}
