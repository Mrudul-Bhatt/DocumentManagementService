using DMS.Domain.Entities;

namespace DMS.Domain.Repositories;

/// <summary>
/// Repository abstraction for FileMetadata aggregate persistence.
///
/// Why is this interface in the Domain layer?
///   Dependency Inversion Principle: command and query handlers in the Application layer
///   depend on this interface, not on EF Core. The Infrastructure layer provides the
///   concrete EF Core implementation. This means handlers can be tested in isolation
///   using an in-memory fake — no database required.
///
/// Why no UpdateAsync?
///   FileMetadata is append-mostly — once a file is uploaded its metadata never changes.
///   There is no rename, no re-upload, no metadata edit in this system. The absence of
///   UpdateAsync is a deliberate statement of that immutability.
/// </summary>
public interface IFileMetadataRepository
{
    /// <summary>
    /// Retrieves file metadata by its primary key. Returns null if no file with that GUID exists.
    ///
    /// Why return null instead of throwing NotFoundException?
    ///   Null propagates through the Result pattern cleanly: the handler maps null →
    ///   DomainErrors.File.NotFound → Result.Failure, returning a typed error without
    ///   exceptions crossing layer boundaries.
    /// </summary>
    Task<FileMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all files belonging to the given user, ordered by upload date descending.
    ///
    /// Why IReadOnlyList<FileMetadata> instead of IEnumerable<FileMetadata>?
    ///   IReadOnlyList guarantees the query is fully materialised before the method returns.
    ///   IEnumerable would allow deferred execution — the database call could happen after
    ///   the DbContext is disposed (end of the request scope), causing a runtime error.
    ///   IReadOnlyList also prevents the caller from adding or removing items, which
    ///   makes the contract clearer.
    /// </summary>
    Task<IReadOnlyList<FileMetadata>> GetByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new FileMetadata entity. The GUID is application-generated (in FileMetadata.Create()),
    /// so no identity value is returned — the caller already has the GUID.
    /// </summary>
    Task AddAsync(FileMetadata file, CancellationToken ct = default);

    /// <summary>
    /// Removes a FileMetadata entity from the database.
    ///
    /// Why accept the entity rather than just the Guid?
    ///   EF Core's Remove() operates on an entity already tracked by the change tracker.
    ///   The delete handler fetches the entity first (to verify ownership via BelongsTo()),
    ///   so it already has the tracked instance. Passing the entity avoids a redundant lookup.
    /// </summary>
    Task DeleteAsync(FileMetadata file, CancellationToken ct = default);
}
