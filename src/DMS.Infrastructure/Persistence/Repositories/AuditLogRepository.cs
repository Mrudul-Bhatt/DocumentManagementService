using DMS.Domain.Entities;
using DMS.Domain.Repositories;

namespace DMS.Infrastructure.Persistence.Repositories;

internal sealed class AuditLogRepository(AppDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
    {
        await dbContext.AuditLogs.AddAsync(log, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
