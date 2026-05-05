using DMS.Api.Extensions;
using DMS.Application.Files.Commands.DeleteFile;
using DMS.Application.Files.Commands.UploadFile;
using DMS.Application.Files.Queries.DownloadFile;
using DMS.Application.Files.Queries.ListFiles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Controllers;

/// <summary>
/// Handles all HTTP operations for file management: upload, list, download, and delete.
///
/// Design contract:
///   This controller contains zero business logic. Every action extracts the caller's
///   identity from the validated JWT, constructs a Command or Query, dispatches it via
///   MediatR, and maps the Result back to an HTTP response. Nothing more.
///   All decisions about ownership, size limits, and empty files live in
///   DMS.Application and DMS.Domain, not here.
///
/// Level 1 changes from Level 0:
///   - [Authorize] added at class level — every endpoint now requires a valid JWT.
///     The X-User-Id trusted header is removed entirely. Identity is cryptographically
///     verified by the JWT Bearer middleware before any action runs.
///   - User.GetUserId() replaces Request.Headers["X-User-Id"] — the user's Id is read
///     from the NameIdentifier claim in the validated token, not from a header.
///   - IP address is extracted and passed into every command/query to support audit
///     logging via the AuditLoggingBehaviour pipeline behaviour.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]   // Applied at class level — all four endpoints require a valid JWT Bearer token.
public sealed class FilesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Uploads a file for the authenticated user.
    ///
    /// Flow:
    ///   1. Extract the caller's user ID from the JWT claim via User.GetUserId().
    ///   2. Extract the caller's IP address for audit logging.
    ///   3. Build an UploadFileCommand with file metadata, the raw stream, and IP.
    ///   4. Dispatch to MediatR — the handler validates size, persists bytes, saves metadata.
    ///   5. On success, return 201 Created with a Location header pointing to the download URL.
    ///
    /// Why [RequestSizeLimit(26_214_400)]?
    ///   Enforces the 25 MB cap at the ASP.NET Core pipeline level before the request body
    ///   reaches the handler. Large payloads are rejected immediately without consuming memory.
    ///   The handler also checks the size as defence-in-depth for non-HTTP callers.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(26_214_400)] // 25 MB in bytes
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct)
    {
        // GetUserId() reads ClaimTypes.NameIdentifier from the validated JWT.
        // Throws InvalidOperationException if the claim is absent — which would be a
        // bug in JwtTokenService, not a normal runtime condition.
        var userId = User.GetUserId().ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // IFormFile.OpenReadStream() returns the raw HTTP request body stream.
        // Passing it directly avoids buffering all file bytes in memory —
        // the handler pipes it straight to disk via IFileStorageService.
        var command = new UploadFileCommand(
            userId,
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream(),
            ip);

        var result = await sender.Send(command, ct);

        // CreatedAtAction generates Location: /api/files/{id} automatically.
        return result.IsFailure
            ? result.ToProblemResult(this)
            : CreatedAtAction(nameof(DownloadFile), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Returns all files belonging to the authenticated user.
    ///
    /// The handler filters by UserId — callers only ever see their own files.
    /// An authenticated user with no uploads receives 200 OK with an empty array.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFiles(CancellationToken ct)
    {
        var userId = User.GetUserId().ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new ListFilesQuery(userId, ip), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Downloads a file by its unique identifier.
    ///
    /// Flow:
    ///   1. Load metadata to resolve the storage path.
    ///   2. Verify the requesting user owns the file — returns 403 if not.
    ///   3. Open the file stream and return it via File(...) for HTTP streaming.
    ///
    /// Why {id:guid} route constraint?
    ///   Rejects requests where the id segment is not a valid GUID at the routing layer,
    ///   before any handler code runs.
    ///
    /// Why File(stream, ...) instead of byte[]?
    ///   ASP.NET Core streams the response directly from storage to the HTTP response body —
    ///   constant memory use regardless of file size (up to 25 MB).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId().ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new DownloadFileQuery(id, userId, ip), ct);

        if (result.IsFailure)
            return result.ToProblemResult(this);

        // File(stream, contentType, fileName) sets Content-Type and Content-Disposition headers.
        // ASP.NET Core disposes the stream after the response body finishes writing.
        return File(result.Value.Content, result.Value.MimeType, result.Value.Filename);
    }

    /// <summary>
    /// Permanently deletes a file by its unique identifier.
    ///
    /// The handler verifies ownership before deleting — a user cannot delete another
    /// user's file, returning 403 Forbidden rather than 404 to avoid leaking existence.
    ///
    /// Why 204 No Content on success?
    ///   The resource no longer exists — there is nothing meaningful to return.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFile(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId().ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new DeleteFileCommand(id, userId, ip), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }
}
