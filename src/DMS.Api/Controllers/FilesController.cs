using DMS.Api.Extensions;
using DMS.Application.Files.Commands.DeleteFile;
using DMS.Application.Files.Commands.DeleteFileVersion;
using DMS.Application.Files.Commands.RestoreFileVersion;
using DMS.Application.Files.Commands.UploadFile;
using DMS.Application.Files.Queries.DownloadFile;
using DMS.Application.Files.Queries.ListFileVersions;
using DMS.Application.Files.Queries.ListFiles;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class FilesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Uploads a file. If a file with the same name already exists in the target folder,
    /// a new version is created instead of a duplicate file.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(26_214_400)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    public async Task<IActionResult> UploadFile(IFormFile file, [FromQuery] Guid? folderId, CancellationToken ct)
    {
        var userId = User.GetUserId().ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var command = new UploadFileCommand(
            userId,
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream(),
            ip,
            folderId);

        var result = await sender.Send(command, ct);

        return result.IsFailure
            ? result.ToProblemResult(this)
            : CreatedAtAction(nameof(DownloadFile), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFiles(CancellationToken ct)
    {
        var userId = User.GetUserId().ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new ListFilesQuery(userId, ip), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

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

        return File(result.Value.Content, result.Value.MimeType, result.Value.Filename);
    }

    /// <summary>
    /// Moves a file to trash (soft-delete). Permanently deleted after 30-day retention.
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

    // ── Versioning endpoints ──────────────────────────────────────────────────────────────

    [HttpGet("{fileId:guid}/versions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListVersions(Guid fileId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new ListFileVersionsQuery(fileId, userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Restores an older version as the current live version.
    /// The file's storage path is updated to point to the chosen version's physical file.
    /// </summary>
    [HttpPost("{fileId:guid}/versions/{versionId:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreVersion(Guid fileId, Guid versionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new RestoreFileVersionCommand(fileId, versionId, userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }

    /// <summary>
    /// Permanently deletes a specific version. Cannot delete the version currently backing the live file.
    /// </summary>
    [HttpDelete("{fileId:guid}/versions/{versionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteVersion(Guid fileId, Guid versionId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new DeleteFileVersionCommand(fileId, versionId, userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }
}
