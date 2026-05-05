namespace DMS.Domain.Services;

/// <summary>
/// Abstraction for binary file storage operations.
///
/// Why a Domain Service rather than a Repository?
///   Repositories model aggregate persistence (rows in a relational database).
///   File content is not a domain aggregate — it is raw bytes managed by an external
///   storage backend (local filesystem in Level 1; Azure Blob Storage or S3 in later
///   levels). A Domain Service interface captures the storage contract without implying
///   a relational structure.
///
/// Why is this interface defined in the Domain layer?
///   Dependency Inversion: the Application layer's upload and download handlers depend
///   on this interface. The Infrastructure layer provides the concrete implementation
///   (LocalFileStorageService). Placing the interface in the Domain layer ensures neither
///   Application nor Domain has a compile-time dependency on Infrastructure — the
///   dependency arrow points inward.
///
/// Why does the interface use Stream rather than byte[]?
///   Streams allow constant-memory processing regardless of file size. If this interface
///   accepted or returned byte[], the full file content would have to be buffered in memory
///   before the method could complete — infeasible for large files near the 25 MB cap.
///   With Stream, bytes flow from HTTP request → storage write and from storage read → HTTP
///   response without ever fully materialising in the application process.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Writes the content stream to durable storage and returns an opaque path string.
    ///
    /// Why return a string path instead of a typed StoragePath object?
    ///   The path format is an implementation detail of the storage backend. A local
    ///   implementation uses a relative file path; a cloud implementation would use a
    ///   blob key or object URL. Returning an opaque string keeps the Domain interface
    ///   backend-agnostic. The returned value is stored on FileMetadata.StoragePath and
    ///   passed back to ReadAsync/DeleteAsync — application code never interprets it.
    ///
    /// Why accept userId and fileId separately instead of a pre-built path?
    ///   The storage service owns path construction. This prevents application code from
    ///   crafting paths with path-traversal characters (e.g., "../") — the service validates
    ///   and sanitises the inputs before building the physical path.
    ///
    /// Stream ownership: the caller (UploadFileCommandHandler) owns the stream and is
    /// responsible for disposing it. SaveAsync reads from the stream but does not close it.
    /// </summary>
    Task<string> SaveAsync(Stream content, string userId, string fileId, CancellationToken ct = default);

    /// <summary>
    /// Opens a readable stream over the stored file content.
    ///
    /// Why return Stream rather than byte[]?
    ///   See class-level comment. The returned stream is owned by the caller: the download
    ///   handler wraps it in a FileDownloadResult DTO, which the controller passes to
    ///   File() — ASP.NET Core disposes the stream after writing the HTTP response body.
    ///
    /// Why throw FileNotFoundException instead of returning Result.Failure when the file is missing?
    ///   A missing physical file when the metadata row exists is a data integrity error,
    ///   not an expected business condition. The metadata row and physical file must always
    ///   be consistent. An exception here signals a bug or storage failure that warrants
    ///   investigation rather than a graceful "not found" response to the client.
    /// </summary>
    Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default);

    /// <summary>
    /// Removes the physical file at the given storage path.
    ///
    /// Why is this idempotent (safe to call on a path that does not exist)?
    ///   The delete flow removes the physical file first, then the metadata row.
    ///   If the process crashes between the two steps and is retried, DeleteAsync would be
    ///   called again on a path that no longer exists. An idempotent implementation with a
    ///   File.Exists guard means the retry succeeds without throwing.
    /// </summary>
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
