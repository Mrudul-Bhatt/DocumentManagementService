using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IFileMetadataRepository.
/// </summary>
internal sealed class FileMetadataRepository(AppDbContext dbContext) : IFileMetadataRepository
{
    /// <summary>
    /// PK lookup via FindAsync — checks the identity map before hitting the database.
    /// Returns null if no FileMetadata row exists with the given GUID.
    /// </summary>
    public async Task<FileMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.FileMetadata.FindAsync([id], ct);

    /// <summary>
    /// Returns all file metadata rows owned by the given user, newest first.
    ///
    /// Why OrderByDescending(f => f.UploadedAt)?
    ///   A client displaying a file list expects the most recently uploaded file at the top.
    ///   Sorting is done here (server-side, in SQL) rather than in the handler to avoid
    ///   loading all rows into memory and sorting in-process — the database can use
    ///   IX_FileMetadata_UserId + UploadedAt sort in the execution plan.
    ///
    /// Why ToListAsync and not AsAsyncEnumerable?
    ///   ToListAsync materialises the full result set before the method returns. The
    ///   IReadOnlyList<T> return type contract requires a fully-materialised, in-memory
    ///   collection — deferred enumeration via IAsyncEnumerable would risk iterating
    ///   after the DbContext scope ends.
    /// </summary>
    public async Task<IReadOnlyList<FileMetadata>> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        await dbContext.FileMetadata
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Inserts a new FileMetadata row and flushes to the database immediately.
    /// </summary>
    public async Task AddAsync(FileMetadata file, CancellationToken ct = default)
    {
        await dbContext.FileMetadata.AddAsync(file, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Deletes a FileMetadata row from the database.
    ///
    /// Why Remove() (synchronous) followed by async SaveChangesAsync?
    ///   Remove() only marks the entity as Deleted in the change tracker — no I/O occurs.
    ///   The actual DELETE statement is issued by SaveChangesAsync. This is EF Core's
    ///   standard pattern: change tracker operations are synchronous; persistence is async.
    ///
    /// Why accept the entity rather than just the Guid?
    ///   The delete handler already has the tracked entity (it fetched it to call BelongsTo()).
    ///   Passing the entity to Remove() allows EF to use its change tracker reference
    ///   directly rather than fetching the row a second time to obtain a tracked instance.
    /// </summary>
    public async Task DeleteAsync(FileMetadata file, CancellationToken ct = default)
    {
        dbContext.FileMetadata.Remove(file);
        await dbContext.SaveChangesAsync(ct);
    }
}
