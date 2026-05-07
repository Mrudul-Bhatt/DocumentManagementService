using DMS.Api.Extensions;
using DMS.Application.Folders.Commands.CreateFolder;
using DMS.Application.Folders.Commands.DeleteFolder;
using DMS.Application.Folders.Commands.MoveFolder;
using DMS.Application.Folders.Commands.RenameFolder;
using DMS.Application.Folders.Commands.RestoreFolder;
using DMS.Application.Folders.Queries.GetFolderContents;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class FoldersController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Returns the contents of the authenticated user's root (no folder selected).
    /// Root-level folders and root-level files are returned.
    /// </summary>
    [HttpGet("root")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoot(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new GetFolderContentsQuery(null, userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Returns the contents (sub-folders and files) of the specified folder.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContents(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new GetFolderContentsQuery(id, userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Creates a new folder. Optionally nested inside a parent folder.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateFolder(CreateFolderRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var command = new CreateFolderCommand(request.Name, request.ParentFolderId, userId, ip);
        var result = await sender.Send(command, ct);

        return result.IsFailure
            ? result.ToProblemResult(this)
            : CreatedAtAction(nameof(GetContents), new { id = result.Value.Id }, result.Value);
    }

    [HttpPatch("{id:guid}/name")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenameFolder(Guid id, RenameFolderRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new RenameFolderCommand(id, request.NewName, userId, ip), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }

    [HttpPatch("{id:guid}/parent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MoveFolder(Guid id, MoveFolderRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await sender.Send(new MoveFolderCommand(id, request.TargetParentFolderId, userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }

    /// <summary>
    /// Moves the folder and all its contents to trash (soft-delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFolder(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new DeleteFolderCommand(id, userId, ip), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreFolder(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new RestoreFolderCommand(id, userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }
}

public sealed record CreateFolderRequest(string Name, Guid? ParentFolderId);
public sealed record RenameFolderRequest(string NewName);
public sealed record MoveFolderRequest(Guid? TargetParentFolderId);
