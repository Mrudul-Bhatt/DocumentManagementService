using DMS.Domain.Entities;

namespace DMS.Domain.Repositories;

/// <summary>
/// Defines the persistence contract for FileMetadata entities.
///
/// Why does this interface live in DMS.Domain instead of DMS.Application or DMS.Infrastructure?
///   This is the Dependency Inversion Principle in practice. The Domain layer defines
///   what it needs from persistence (the interface); the Infrastructure layer satisfies
///   that need (the EF Core implementation). This keeps the dependency arrow pointing
///   inward: Infrastructure depends on Domain, never the other way around.
///   If this interface lived in Infrastructure, the Application layer would need to
///   reference Infrastructure to use it — which would collapse the layer boundary and
///   let application handlers take direct dependencies on EF Core, SQL, and other
///   infrastructure concerns.
///
/// Why the Repository pattern instead of using DbContext directly in handlers?
///   Handlers that depend on IFileMetadataRepository can be unit tested with a simple
///   in-memory fake or mock — no database or EF Core setup required. Handlers that
///   depend on DbContext directly cannot be tested without standing up the full
///   infrastructure stack, making tests slow, brittle, and environment-dependent.
///   The repository also gives queries a named home: GetByUserIdAsync is explicit
///   and searchable; a raw LINQ expression inside a handler is opaque.
///
/// Why no UpdateAsync method?
///   FileMetadata is append-mostly: files are created and deleted, never partially
///   updated. There is no business operation that changes a file's metadata after
///   upload. Adding UpdateAsync with no caller would be speculative design.
///   If a rename or re-tag feature is added in a future level, UpdateAsync
///   belongs here at that point.
/// </summary>
public interface IFileMetadataRepository
{
    /// <summary>
    /// Returns the file metadata record with the given id, or null if it does not exist.
    ///
    /// Why return null instead of throwing on not-found?
    ///   Not-found is an expected, normal outcome — not an exceptional condition.
    ///   Returning null forces the caller to handle both the found and not-found
    ///   cases explicitly. The handler converts null into Result.Failure(DomainErrors.File.NotFound),
    ///   which produces a clean 404 response rather than a caught exception.
    /// </summary>
    Task<FileMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns all file metadata records owned by the given user.
    ///
    /// Returns an empty list (never null) when the user has no files.
    /// The caller should never need to null-check the result — an empty
    /// collection is the correct representation of "no files".
    ///
    /// Why IReadOnlyList instead of IEnumerable?
    ///   IReadOnlyList guarantees the query has already been executed and the
    ///   results are materialised in memory. Returning IEnumerable would leave
    ///   open the possibility of deferred execution, where the database query
    ///   runs lazily outside the repository — after the DbContext's unit of work
    ///   has been disposed, causing a runtime exception.
    /// </summary>
    Task<IReadOnlyList<FileMetadata>> GetByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new FileMetadata entity to the database.
    ///
    /// The entity must have been created via FileMetadata.Create() — the repository
    /// does not validate the entity's state, it trusts the Domain factory to have
    /// produced a valid instance.
    /// </summary>
    Task AddAsync(FileMetadata file, CancellationToken ct = default);

    /// <summary>
    /// Permanently removes the given FileMetadata entity from the database.
    ///
    /// Why accept the full entity instead of just the Guid id?
    ///   EF Core's change tracker needs a tracked entity reference to issue a DELETE.
    ///   Passing the entity the handler already loaded avoids a redundant SELECT
    ///   inside the repository. The handler is responsible for the ownership check
    ///   before calling this method — the repository performs no authorisation.
    /// </summary>
    Task DeleteAsync(FileMetadata file, CancellationToken ct = default);
}
