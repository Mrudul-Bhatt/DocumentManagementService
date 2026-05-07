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
    /// Removes a FileMetadata entity from the database (hard delete).
    /// Only called when permanently purging a trashed file — not for the normal delete flow,
    /// which uses SoftDelete() + UpdateAsync().
    /// </summary>
    Task DeleteAsync(FileMetadata file, CancellationToken ct = default);

    /// <summary>Persists mutations to an existing FileMetadata entity (e.g. after SoftDelete, Restore, MoveTo, UpdateStoragePath).</summary>
    Task UpdateAsync(FileMetadata file, CancellationToken ct = default);

    /// <summary>Returns active (non-deleted) files in the specified folder. Pass null folderId for root-level files.</summary>
    Task<IReadOnlyList<FileMetadata>> GetByFolderIdAsync(string userId, Guid? folderId, CancellationToken ct = default);

    /// <summary>Returns all soft-deleted files for the user (ignores the global DeletedAt filter).</summary>
    Task<IReadOnlyList<FileMetadata>> GetDeletedByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>Loads a single soft-deleted file by ID (ignores the global filter). Returns null if not found or not deleted.</summary>
    Task<FileMetadata?> GetDeletedByIdAsync(Guid id, CancellationToken ct = default);
}
