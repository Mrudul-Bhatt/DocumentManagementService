using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DMS.Application.Files.Commands.DeleteFile;

/// <summary>
/// Handles the DeleteFileCommand: verifies the file exists and is owned by the
/// requesting user, then deletes the bytes from storage and the record from the database.
/// </summary>
internal sealed class DeleteFileCommandHandler(
    IFileStorageService storageService,
    IFileMetadataRepository repository,
    ILogger<DeleteFileCommandHandler> logger)
    : IRequestHandler<DeleteFileCommand, Result>
{
    /// <summary>
    /// Executes the delete operation.
    ///
    /// Flow:
    ///   1. Load the file metadata record from the database.
    ///   2. Return 404 if the record does not exist.
    ///   3. Return 403 if the requesting user does not own the file.
    ///   4. Delete the bytes from storage.
    ///   5. Delete the metadata record from the database.
    ///   6. Log the deletion event.
    ///
    /// Why check ownership via metadata.BelongsTo() rather than in the query?
    ///   Ownership is a domain rule — it belongs on the entity, not in a LINQ predicate
    ///   or SQL WHERE clause. BelongsTo() centralises that rule so it cannot be
    ///   accidentally omitted in future handlers that also load files.
    ///
    /// Why return 403 instead of 404 when the file exists but belongs to another user?
    ///   Returning 404 for both "not found" and "wrong owner" would hide the distinction
    ///   and is a common security pattern to avoid leaking resource existence.
    ///   However, at Level 0 (trusted X-User-Id header) there is no real security boundary
    ///   anyway. The 403/404 split is implemented correctly here as the foundation for
    ///   the authenticated Level 1 model where this distinction genuinely matters.
    ///
    /// Why delete storage before the database record?
    ///   If the storage delete succeeds but the DB delete fails, the metadata record
    ///   remains and points to a file that no longer exists — a stale reference.
    ///   If the DB delete succeeds but storage delete fails, bytes are orphaned on disk
    ///   but the metadata is gone — no pointer to clean them up.
    ///   Neither order is fully safe without a distributed transaction. At this level,
    ///   storage-first is chosen because orphaned bytes are easier to detect and clean
    ///   up (a periodic sweep of unreferenced files) than stale DB records pointing at
    ///   missing files that would cause errors on every access attempt.
    /// </summary>
    public async Task<Result> Handle(DeleteFileCommand command, CancellationToken ct)
    {
        var metadata = await repository.GetByIdAsync(command.FileId, ct);

        if (metadata is null)
            return Result.Failure(DomainErrors.File.NotFound);

        // BelongsTo() is a domain method on FileMetadata — ownership logic lives
        // on the entity, not scattered across handlers or repository queries.
        if (!metadata.BelongsTo(command.UserId))
            return Result.Failure(DomainErrors.File.Forbidden);

        // Delete bytes first — see method summary for the ordering rationale.
        await storageService.DeleteAsync(metadata.StoragePath, ct);
        await repository.DeleteAsync(metadata, ct);

        logger.LogInformation("File {FileId} deleted by user {UserId}", command.FileId, command.UserId);

        return Result.Success();
    }
}
