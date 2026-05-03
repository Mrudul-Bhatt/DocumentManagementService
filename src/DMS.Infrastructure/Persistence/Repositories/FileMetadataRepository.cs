using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IFileMetadataRepository.
///
/// Why internal sealed?
///   This is an infrastructure implementation detail. The Application layer depends
///   only on the IFileMetadataRepository interface defined in DMS.Domain — it has
///   no knowledge that EF Core or this class exists. internal prevents accidental
///   direct references from Application or API layers; sealed prevents subclassing.
///
/// Why primary constructor injection (AppDbContext dbContext)?
///   AppDbContext is Scoped (one instance per HTTP request). Injecting it via the
///   primary constructor ties the repository's lifetime to the same scope, ensuring
///   all operations within one request share the same DbContext instance and its
///   identity map and change tracker. This is the standard EF Core usage pattern.
/// </summary>
internal sealed class FileMetadataRepository(AppDbContext dbContext) : IFileMetadataRepository
{
    /// <summary>
    /// Fetches a single record by primary key, returning null if not found.
    ///
    /// Why FindAsync instead of FirstOrDefaultAsync(f => f.Id == id)?
    ///   FindAsync checks the DbContext's identity map first — if the entity was
    ///   already loaded earlier in the same request, it is returned from memory
    ///   without hitting the database. FirstOrDefaultAsync always issues a SELECT.
    ///   For primary key lookups, FindAsync is the correct and more efficient choice.
    /// </summary>
    public async Task<FileMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.FileMetadata.FindAsync([id], ct);

    /// <summary>
    /// Returns all records owned by the given user, newest first.
    ///
    /// Why OrderByDescending(UploadedAt)?
    ///   Most-recent-first is the natural display order for a file list — users
    ///   expect to see their latest uploads at the top. The sort is applied in the
    ///   database via ORDER BY (translated by EF Core) rather than in-memory, so
    ///   it leverages the database engine's optimised sort on the indexed column.
    ///
    /// Why ToListAsync() to satisfy IReadOnlyList?
    ///   ToListAsync() materialises the query immediately inside this method,
    ///   ensuring the database connection is used and released while the DbContext
    ///   is still in scope. The resulting List<T> satisfies IReadOnlyList<T>
    ///   via implicit interface implementation. Returning IQueryable or IEnumerable
    ///   would defer execution past the repository boundary, risking a disposed
    ///   DbContext when the query eventually runs.
    /// </summary>
    public async Task<IReadOnlyList<FileMetadata>> GetByUserIdAsync(string userId, CancellationToken ct = default) =>
        await dbContext.FileMetadata
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Adds a new FileMetadata record to the database.
    ///
    /// Why SaveChangesAsync immediately after AddAsync?
    ///   This repository does not participate in a larger unit of work — each
    ///   operation is its own transaction. Calling SaveChangesAsync here flushes
    ///   the INSERT immediately. If a higher-level unit of work pattern were
    ///   introduced (e.g., a shared transaction across multiple repositories),
    ///   SaveChangesAsync would move to the unit-of-work boundary and be removed here.
    ///
    /// Why AddAsync instead of Add?
    ///   AddAsync is preferred when a value generator may need to communicate with
    ///   the database asynchronously (e.g., HiLo sequences). For application-generated
    ///   GUIDs (ValueGeneratedNever), Add and AddAsync are functionally identical —
    ///   AddAsync is used here for consistency with the async pattern throughout.
    /// </summary>
    public async Task AddAsync(FileMetadata file, CancellationToken ct = default)
    {
        await dbContext.FileMetadata.AddAsync(file, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Removes the given FileMetadata record from the database.
    ///
    /// Why Remove (synchronous) instead of an async equivalent?
    ///   Remove only marks the entity as Deleted in the DbContext's change tracker —
    ///   no I/O occurs at this point. The actual DELETE statement is issued
    ///   asynchronously when SaveChangesAsync is called on the next line.
    ///   There is no async overload for Remove because marking an entity deleted
    ///   is a pure in-memory operation.
    /// </summary>
    public async Task DeleteAsync(FileMetadata file, CancellationToken ct = default)
    {
        dbContext.FileMetadata.Remove(file);
        await dbContext.SaveChangesAsync(ct);
    }
}
