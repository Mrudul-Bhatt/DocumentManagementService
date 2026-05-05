using DMS.Domain.Services;
using Microsoft.Extensions.Configuration;

namespace DMS.Infrastructure.Storage;

/// <summary>
/// Local filesystem implementation of IFileStorageService.
///
/// This is the Level 1 storage backend: files are written to a directory on the same
/// machine running the API. It is suitable for local development and single-server
/// deployments. A future level will replace this with a cloud blob storage provider
/// (e.g., Azure Blob Storage) by registering a different IFileStorageService implementation
/// in DependencyInjection — no other code changes are required thanks to the interface.
///
/// Why IConfiguration is acceptable here (unlike in Application)?
///   IConfiguration is an infrastructure concern. Injecting it into an Infrastructure
///   class is appropriate. Application layer classes use the Options pattern (IOptions<T>)
///   instead because they should not take a direct dependency on the configuration system.
///
/// Why internal sealed?
///   Infrastructure implementation detail; consumers depend on IFileStorageService.
///   sealed prevents subclassing within Infrastructure.
/// </summary>
internal sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    /// <summary>
    /// Root directory for all uploaded files. Read from appsettings ("Storage:UploadRoot").
    /// Falls back to an "uploads" folder in the application's current working directory
    /// if the config key is absent — safe default for local development without config.
    ///
    /// Why a computed property (=>) instead of a field?
    ///   IConfiguration is re-read on each access, which supports runtime config refresh
    ///   in environments that use IConfiguration reloading. For a static path this is
    ///   a minor point, but the pattern avoids caching a value that could theoretically change.
    /// </summary>
    private string UploadRoot => configuration["Storage:UploadRoot"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    /// <summary>
    /// Writes the file content stream to {UploadRoot}/{userId}/{fileId} and returns the path.
    ///
    /// Why {userId} subdirectory?
    ///   Organises files by owner, making manual filesystem inspection and potential
    ///   per-user storage cleanup straightforward. Keeps the upload root from becoming a
    ///   flat directory with millions of files.
    ///
    /// Why fileId (the GUID string) as the filename, not the original filename?
    ///   Two reasons: (1) Collision safety — two users uploading "report.pdf" would
    ///   overwrite each other's file if the original name were used. GUIDs are unique.
    ///   (2) Path traversal prevention — a malicious client could send a filename like
    ///   "../../etc/passwd". Using the server-generated GUID makes client input irrelevant
    ///   to path construction.
    ///
    /// Why Directory.CreateDirectory instead of checking File.Exists first?
    ///   Directory.CreateDirectory is idempotent — it does nothing if the directory already
    ///   exists. No race condition between check and create.
    ///
    /// Why FileShare.None during write?
    ///   Exclusive write lock prevents a concurrent read of a partially-written file.
    ///   A reader that opens the file while it is being written would receive incomplete bytes.
    ///
    /// await using: the FileStream is disposed immediately after CopyToAsync completes,
    /// flushing the OS write buffer before the method returns.
    /// </summary>
    public async Task<string> SaveAsync(Stream content, string userId, string fileId, CancellationToken ct = default)
    {
        var directory = Path.Combine(UploadRoot, userId);
        Directory.CreateDirectory(directory);

        var storagePath = Path.Combine(directory, fileId);

        await using var fileStream = new FileStream(storagePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, ct);

        return storagePath;
    }

    /// <summary>
    /// Opens a read-only FileStream over the stored file. The caller owns the stream
    /// and is responsible for disposing it (ASP.NET Core disposes it after writing the response).
    ///
    /// Why FileShare.Read?
    ///   Allows multiple concurrent download requests to open the same file simultaneously.
    ///   FileShare.None would serialise downloads — the second concurrent request would
    ///   throw an IOException while the first download is still in progress.
    ///
    /// Why throw FileNotFoundException instead of returning a Result failure?
    ///   A missing physical file when the metadata row exists is a data integrity violation —
    ///   it indicates the files are out of sync (e.g., a manual filesystem deletion or a
    ///   failed upload that left a metadata row). This is a programming or operational error,
    ///   not an expected business condition. GlobalExceptionMiddleware catches the exception
    ///   and returns 500; the on-call engineer investigates the inconsistency.
    ///
    /// Why Task.FromResult instead of async/await?
    ///   Opening a FileStream is synchronous — there is no I/O to await. Wrapping the
    ///   synchronous result in Task.FromResult satisfies the async interface contract
    ///   without the overhead of allocating a state machine.
    /// </summary>
    public Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default)
    {
        if (!File.Exists(storagePath))
            throw new FileNotFoundException("File not found on storage.", storagePath);

        Stream stream = new FileStream(storagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    /// <summary>
    /// Deletes the physical file at the given storage path.
    ///
    /// Why File.Exists guard before File.Delete?
    ///   Idempotency for crash recovery. The delete flow removes the physical file first,
    ///   then removes the metadata row. If the process crashes between the two steps and
    ///   the operation is retried, DeleteAsync is called again on a path that no longer
    ///   exists. Without the guard, File.Delete would throw FileNotFoundException on retry.
    ///   With the guard, the method returns successfully and the metadata delete can proceed.
    ///
    /// Why Task.CompletedTask?
    ///   File.Delete is synchronous. Task.CompletedTask is a pre-allocated singleton
    ///   representing an already-completed task — returning it avoids allocating a new
    ///   Task object on each call.
    /// </summary>
    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        if (File.Exists(storagePath))
            File.Delete(storagePath);

        return Task.CompletedTask;
    }
}
