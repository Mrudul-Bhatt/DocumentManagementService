using DMS.Api.Extensions;
using DMS.Application.DTOs;
using DMS.Application.Trash.Commands.EmptyTrash;
using DMS.Application.Trash.Commands.RestoreFromTrash;
using DMS.Application.Trash.Queries.ListTrash;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class TrashController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Returns all soft-deleted files and folders for the authenticated user,
    /// ordered by deletion date descending (most recently deleted first).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTrash(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new ListTrashQuery(userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Restores a single trashed item (file or folder) back to its original location.
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreItem(Guid id, RestoreItemRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new RestoreFromTrashCommand(id, request.ItemType, userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }

    /// <summary>
    /// Permanently deletes all trashed items for the authenticated user.
    /// Physical files are deleted from storage immediately.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EmptyTrash(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new EmptyTrashCommand(userId), ct);
        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }
}

public sealed record RestoreItemRequest(TrashedItemType ItemType);
