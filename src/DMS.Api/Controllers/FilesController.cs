using DMS.Api.Extensions;
using DMS.Application.Files.Commands.DeleteFile;
using DMS.Application.Files.Commands.UploadFile;
using DMS.Application.Files.Queries.DownloadFile;
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
    [HttpPost]
    [RequestSizeLimit(26_214_400)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413RequestEntityTooLarge)]
    public async Task<IActionResult> UploadFile(IFormFile file, CancellationToken ct)
    {
        var userId = User.GetUserId().ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var command = new UploadFileCommand(
            userId,
            file.FileName,
            file.ContentType,
            file.Length,
            file.OpenReadStream(),
            ip);

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
