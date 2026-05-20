using DMS.Domain.Entities;
using DMS.Domain.Enums;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

internal sealed class PublicLinkRepository(AppDbContext db) : IPublicLinkRepository
{
    public async Task<PublicLink?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.PublicLinks.FindAsync([id], ct);

    public async Task<PublicLink?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        await db.PublicLinks
            .FirstOrDefaultAsync(l => l.Token == token, ct);

    public async Task<IReadOnlyList<PublicLink>> GetByResourceAsync(Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default) =>
        await db.PublicLinks
            .Where(l => l.ResourceId == resourceId && l.ResourceType == resourceType)
            .ToListAsync(ct);

    public async Task AddAsync(PublicLink link, CancellationToken ct = default)
    {
        await db.PublicLinks.AddAsync(link, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(PublicLink link, CancellationToken ct = default)
    {
        db.PublicLinks.Remove(link);
        await db.SaveChangesAsync(ct);
    }
}
