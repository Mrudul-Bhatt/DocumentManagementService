using DMS.Api.Extensions;
using DMS.Application.Shares.Commands.CreateShare;
using DMS.Application.Shares.Commands.RevokeShare;
using DMS.Application.Shares.Queries.ListShares;
using DMS.Application.Shares.Queries.ListSharedWithMe;
using DMS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SharesController(ISender sender) : ControllerBase
{
    /// <summary>Grants access to a resource for another user by email.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateShare([FromBody] CreateShareRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new CreateShareCommand(
            request.ResourceId,
            request.ResourceType,
            request.GrantedToEmail,
            request.Role,
            userId,
            ipAddress), ct);

        return result.IsFailure
            ? result.ToProblemResult(this)
            : CreatedAtAction(nameof(ListShares), new { resourceId = result.Value.ResourceId, resourceType = result.Value.ResourceType }, result.Value);
    }

    /// <summary>Revokes a share grant. Only the granter or grantee may revoke.</summary>
    [HttpDelete("{shareId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeShare(Guid shareId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await sender.Send(new RevokeShareCommand(shareId, userId, ipAddress), ct);

        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }

    /// <summary>Lists all shares for a specific resource. Only the resource owner may call this.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListShares(
        [FromQuery] Guid resourceId,
        [FromQuery] ShareResourceType resourceType,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new ListSharesQuery(resourceId, resourceType, userId), ct);

        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>Lists all resources that have been shared with the authenticated user.</summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSharedWithMe(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new ListSharedWithMeQuery(userId), ct);

        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }
}

public sealed record CreateShareRequest(
    Guid ResourceId,
    ShareResourceType ResourceType,
    string GrantedToEmail,
    ShareRole Role);
