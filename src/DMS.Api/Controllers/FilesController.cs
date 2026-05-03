using DMS.Api.Extensions;
using DMS.Application.Common;
using DMS.Application.Files.Commands.DeleteFile;
using DMS.Application.Files.Commands.UploadFile;
using DMS.Application.Files.Queries.DownloadFile;
using DMS.Application.Files.Queries.ListFiles;
using DMS.Domain.Errors;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Controllers;

/// <summary>
/// Handles all HTTP operations for file management: upload, list, download, and delete.
///
/// Design contract:
///   - This controller contains zero business logic. Every action extracts the caller's
///     identity from the request, constructs a Command or Query, dispatches it via MediatR,
///     and maps the Result back to an HTTP response. Nothing more.
///   - All decisions about what is allowed (ownership, size limits, empty files) live in
///     DMS.Application and DMS.Domain, not here.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class FilesController(ISender sender) : ControllerBase
{
    // Header name the caller must supply to identify themselves.
    // Level 0: identity is trusted on faith — no cryptographic verification.
    // Level 1 replaces this with JWT bearer authentication.
    private const string UserIdHeader = "X-User-Id";

    /// <summary>
    /// Uploads a file for the requesting user.
    ///
    /// Flow:
    ///   1. Extract and validate the caller's user ID from the custom header.
    ///   2. Build an UploadFileCommand with all file metadata and the raw stream.
    ///   3. Dispatch to MediatR — the handler validates size, persists bytes, saves metadata.
    ///   4. On success, return 201 Created with a Location header pointing to the download URL.
    ///
    /// Why 201 + CreatedAtAction?
    ///   REST convention: after creating a resource, tell the client where to find it.
    ///   CreatedAtAction generates the Location header automatically from the route.
    ///
    /// Why [RequestSizeLimit]?
    ///   Enforces the 25 MB cap at the ASP.NET Core pipeline level, before the request body
    ///   reaches the handler. Large payloads are rejected immediately without consuming memory.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(26_214_400)] // 25 MB in bytes
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct)
    {
        // Fail fast: every file operation requires a caller identity.
        // Returning immediately avoids wasting application and infrastructure resources
        // on requests that cannot possibly succeed.
        var userId = Request.Headers[UserIdHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure(DomainErrors.User.IdMissing).ToProblemResult(this);

        // IFormFile.OpenReadStream() returns the raw request body stream.
        // The stream is passed into the command rather than reading all bytes here —
        // this allows the handler to stream directly to disk without buffering in memory.
        var command = new UploadFileCommand(
            userId,
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream());

        var result = await sender.Send(command, ct);

        // CreatedAtAction(nameof(DownloadFile), ...) generates:
        //   Location: /api/files/{id}
        // The returned DTO is the response body — the new file's metadata.
        return result.IsFailure
            ? result.ToProblemResult(this)
            : CreatedAtAction(nameof(DownloadFile), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Returns all files belonging to the requesting user.
    ///
    /// The handler filters by UserId — callers only ever see their own files.
    /// Ownership enforcement lives in the query handler, not here.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListFiles(CancellationToken ct)
    {
        var userId = Request.Headers[UserIdHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure(DomainErrors.User.IdMissing).ToProblemResult(this);

        var result = await sender.Send(new ListFilesQuery(userId), ct);

        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Downloads a file by its unique identifier.
    ///
    /// Flow:
    ///   1. Load metadata from the database to resolve the storage path.
    ///   2. Verify the requesting user owns the file — returns 403 if not.
    ///   3. Open the file stream from storage.
    ///   4. Return the stream via File(...), which sets Content-Type and Content-Disposition.
    ///
    /// Why {id:guid} route constraint?
    ///   Rejects requests where the id segment is not a valid GUID at the routing layer,
    ///   before any handler code runs. No manual parsing or 400 error handling needed.
    ///
    /// Why return File(stream, ...) instead of byte[]?
    ///   ASP.NET Core streams the response directly from the storage stream to the HTTP
    ///   response body — constant memory use regardless of file size (up to 25 MB).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        var userId = Request.Headers[UserIdHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure(DomainErrors.User.IdMissing).ToProblemResult(this);

        var result = await sender.Send(new DownloadFileQuery(id, userId), ct);

        if (result.IsFailure)
            return result.ToProblemResult(this);

        // File(stream, contentType, fileName) sets:
        //   Content-Type: <mimeType>
        //   Content-Disposition: attachment; filename="<filename>"
        // ASP.NET Core closes the stream after the response completes.
        return File(result.Value.Content, result.Value.MimeType, result.Value.Filename);
    }

    /// <summary>
    /// Permanently deletes a file by its unique identifier.
    ///
    /// The handler verifies ownership before deleting. A user cannot delete
    /// another user's file — the domain returns File.Forbidden in that case.
    ///
    /// Why 204 No Content on success?
    ///   REST convention for a successful delete: the resource no longer exists,
    ///   so there is nothing meaningful to return in the response body.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken ct)
    {
        var userId = Request.Headers[UserIdHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(userId))
            return Result.Failure(DomainErrors.User.IdMissing).ToProblemResult(this);

        var result = await sender.Send(new DeleteFileCommand(id, userId), ct);

        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }
}
