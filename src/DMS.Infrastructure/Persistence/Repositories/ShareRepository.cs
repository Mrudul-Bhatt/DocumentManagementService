using DMS.Domain.Entities;
using DMS.Domain.Enums;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

internal sealed class ShareRepository(AppDbContext db) : IShareRepository
{
    public async Task<Share?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Shares.FindAsync([id], ct);

    public async Task<IReadOnlyList<Share>> GetByResourceAsync(Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default) =>
        await db.Shares
            .Where(s => s.ResourceId == resourceId && s.ResourceType == resourceType)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Share>> GetForUserAsync(Guid userId, CancellationToken ct = default) =>
        await db.Shares
            .Where(s => s.GrantedToUserId == userId)
            .ToListAsync(ct);

    public async Task<Share?> GetDirectShareAsync(Guid resourceId, ShareResourceType resourceType, Guid userId, CancellationToken ct = default) =>
        await db.Shares
            .FirstOrDefaultAsync(s => s.ResourceId == resourceId && s.ResourceType == resourceType && s.GrantedToUserId == userId, ct);

    public async Task<int> CountByResourceAsync(Guid resourceId, ShareResourceType resourceType, CancellationToken ct = default) =>
        await db.Shares
            .CountAsync(s => s.ResourceId == resourceId && s.ResourceType == resourceType, ct);

    public async Task AddAsync(Share share, CancellationToken ct = default)
    {
        await db.Shares.AddAsync(share, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Share share, CancellationToken ct = default)
    {
        db.Shares.Remove(share);
        await db.SaveChangesAsync(ct);
    }
}
