using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Files.Queries.DownloadFile;

/// <summary>
/// Handles the DownloadFileQuery: verifies the file exists and is owned by the
/// requesting user, then opens the file stream and returns it for HTTP streaming.
/// </summary>
internal sealed class DownloadFileQueryHandler(
    IFileStorageService storageService,
    IFileMetadataRepository repository)
    : IRequestHandler<DownloadFileQuery, Result<FileDownloadResult>>
{
    /// <summary>
    /// Executes the download operation.
    ///
    /// Flow:
    ///   1. Load the file metadata record to resolve the storage path.
    ///   2. Return 404 if the record does not exist.
    ///   3. Return 403 if the requesting user does not own the file.
    ///   4. Open the file stream from storage via the resolved StoragePath.
    ///   5. Return the open stream bundled with Filename and MimeType in a FileDownloadResult.
    ///
    /// Why not inject ILogger here (unlike upload/delete handlers)?
    ///   Downloads are high-frequency read operations. Logging every download
    ///   would produce disproportionate log volume with low signal value compared
    ///   to writes. If download auditing is needed in a future level, it belongs
    ///   in an AuditLoggingBehaviour (pipeline behaviour) that runs selectively
    ///   for requests marked with IAuditableRequest — not as inline logging here.
    ///
    /// Stream lifetime:
    ///   ReadAsync opens the file and returns a stream. This handler does not close
    ///   it — ownership transfers to FileDownloadResult and then to the controller,
    ///   which passes it to ASP.NET Core's File() helper. ASP.NET Core disposes the
    ///   stream automatically after the response body finishes writing.
    /// </summary>
    public async Task<Result<FileDownloadResult>> Handle(DownloadFileQuery query, CancellationToken ct)
    {
        var metadata = await repository.GetByIdAsync(query.FileId, ct);

        if (metadata is null)
            return Result.Failure<FileDownloadResult>(DomainErrors.File.NotFound);

        if (!metadata.BelongsTo(query.UserId))
            return Result.Failure<FileDownloadResult>(DomainErrors.File.Forbidden);

        // ReadAsync opens the file at metadata.StoragePath and returns a readable stream.
        // The path was recorded at upload time by IFileStorageService.SaveAsync().
        var stream = await storageService.ReadAsync(metadata.StoragePath, ct);

        return Result.Success(new FileDownloadResult(stream, metadata.Filename, metadata.MimeType));
    }
}
