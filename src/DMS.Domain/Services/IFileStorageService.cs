namespace DMS.Domain.Services;

/// <summary>
/// Defines the contract for reading and writing raw file bytes to a storage backend.
///
/// Why a Domain Service interface instead of a Repository?
///   IFileMetadataRepository manages structured data (database records with identity,
///   queryable fields, relationships). IFileStorageService manages unstructured binary
///   data (raw byte streams keyed by an opaque path string). These are fundamentally
///   different concerns — mixing them into one interface would conflate two distinct
///   infrastructure responsibilities. Domain Services are the right abstraction for
///   operations that don't map cleanly to entity CRUD.
///
/// Why does this interface live in DMS.Domain?
///   Same Dependency Inversion rationale as IFileMetadataRepository: the Application
///   layer handlers need to call storage operations, but must not depend on
///   Infrastructure. Defining the interface in Domain keeps the dependency arrow
///   pointing inward. The concrete implementation (LocalFileStorageService) lives in
///   Infrastructure and is wired up via DI — handlers never reference it directly.
///
/// Storage backend abstraction:
///   Level 0 implements this with local filesystem storage (LocalFileStorageService).
///   Future levels can swap the implementation for Azure Blob Storage, AWS S3, or any
///   other backend by providing a new class that implements this interface and updating
///   the DI registration — no handler or domain code changes required.
///
/// Why storagePath as a string key instead of a typed value object?
///   At this level, a plain string is sufficient. The path is written by SaveAsync,
///   stored verbatim in FileMetadata.StoragePath, and passed back into ReadAsync and
///   DeleteAsync. Neither the Domain nor Application layers interpret or construct the
///   path — it is opaque to them. A value object would add complexity with no benefit
///   until the storage model becomes more sophisticated (e.g., multi-bucket routing).
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Writes the given stream to the storage backend and returns the storage path.
    ///
    /// The returned path is an opaque key — it is stored in FileMetadata.StoragePath
    /// and used verbatim in subsequent ReadAsync and DeleteAsync calls. The Application
    /// layer never interprets or constructs this value.
    ///
    /// Why accept userId and fileId as separate parameters?
    ///   The implementation uses them to build a namespaced directory structure:
    ///   uploads/{userId}/{fileId}. Namespacing by userId isolates each user's files
    ///   on disk, making per-user storage inspection and cleanup straightforward.
    ///   Passing them separately keeps the interface's concerns clear — the caller
    ///   supplies identity context; the implementation decides how to translate it
    ///   into a physical path.
    ///
    /// Stream ownership:
    ///   The caller (handler) owns the stream and is responsible for its lifecycle.
    ///   SaveAsync reads from the stream but does not close or dispose it.
    /// </summary>
    Task<string> SaveAsync(Stream content, string userId, string fileId, CancellationToken ct = default);

    /// <summary>
    /// Opens and returns a readable stream for the file at the given storage path.
    ///
    /// Stream ownership:
    ///   The caller takes ownership of the returned stream and is responsible for
    ///   ensuring it is disposed. In practice, the stream is passed through
    ///   FileDownloadResult to the controller, which passes it to ASP.NET Core's
    ///   File() helper — ASP.NET Core disposes it after the HTTP response finishes.
    ///
    /// Why return Stream instead of byte[]?
    ///   The stream is piped directly to the HTTP response body by ASP.NET Core,
    ///   giving constant memory usage regardless of file size. Loading the entire
    ///   file into a byte[] would spike heap allocation on every download request.
    /// </summary>
    Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default);

    /// <summary>
    /// Permanently removes the file at the given storage path from the backend.
    ///
    /// Called by DeleteFileCommandHandler after the ownership check passes and
    /// before the metadata record is removed from the database. See the handler's
    /// summary for the rationale behind this ordering.
    ///
    /// The implementation should treat a missing file as a no-op rather than
    /// throwing — idempotent deletes prevent errors when retrying a partially
    /// failed operation.
    /// </summary>
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
