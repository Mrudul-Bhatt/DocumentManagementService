using DMS.Domain.Entities;
using DMS.Domain.Repositories;

namespace DMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IAuditLogRepository.
///
/// Why only one method?
///   This implementation is a direct mirror of the IAuditLogRepository interface, which
///   intentionally exposes only AddAsync. The append-only constraint is structural —
///   there is no Update or Delete method to implement because none exists on the interface.
///   See IAuditLogRepository comments for the full rationale.
/// </summary>
internal sealed class AuditLogRepository(AppDbContext dbContext) : IAuditLogRepository
{
    /// <summary>
    /// Inserts a new AuditLog row. The only write operation this repository ever performs.
    ///
    /// Why no try/catch here?
    ///   The caller (AuditLoggingBehaviour) wraps the call in a try/catch that swallows
    ///   exceptions and logs a warning — audit failure must not propagate as a 500 to
    ///   the client. The repository itself is intentionally simple: write, save, done.
    ///   Error handling belongs at the behaviour level, not the repository level.
    /// </summary>
    public async Task AddAsync(AuditLog log, CancellationToken ct = default)
    {
        await dbContext.AuditLogs.AddAsync(log, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
