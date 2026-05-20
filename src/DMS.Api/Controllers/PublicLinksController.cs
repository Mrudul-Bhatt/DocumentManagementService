using DMS.Api.Extensions;
using DMS.Application.PublicLinks.Commands.CreatePublicLink;
using DMS.Application.PublicLinks.Commands.RevokePublicLink;
using DMS.Application.PublicLinks.Queries.ListPublicLinks;
using DMS.Application.PublicLinks.Queries.ResolvePublicLink;
using DMS.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Api.Controllers;

[ApiController]
[Route("api/public")]
public sealed class PublicLinksController(ISender sender) : ControllerBase
{
    /// <summary>Creates a public link for a file or folder. Authenticated users only.</summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePublicLink([FromBody] CreatePublicLinkRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var result = await sender.Send(new CreatePublicLinkCommand(
            request.ResourceId,
            request.ResourceType,
            request.Role,
            userId,
            request.ExpiresAt,
            request.Password), ct);

        return result.IsFailure
            ? result.ToProblemResult(this)
            : CreatedAtAction(nameof(ResolvePublicLink), new { token = result.Value.Token }, result.Value);
    }

    /// <summary>Revokes a public link. Only the creator may revoke.</summary>
    [HttpDelete("{linkId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokePublicLink(Guid linkId, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new RevokePublicLinkCommand(linkId, userId), ct);

        return result.IsFailure ? result.ToProblemResult(this) : NoContent();
    }

    /// <summary>Lists all public links for a resource. Only the resource owner may call this.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListPublicLinks(
        [FromQuery] Guid resourceId,
        [FromQuery] ShareResourceType resourceType,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var result = await sender.Send(new ListPublicLinksQuery(resourceId, resourceType, userId), ct);

        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Resolves a public link token and returns the link details if valid.
    /// This endpoint is unauthenticated — no [Authorize] — so anyone with the token can call it.
    /// </summary>
    [HttpGet("{token}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> ResolvePublicLink(
        string token,
        [FromQuery] string? password,
        CancellationToken ct)
    {
        var result = await sender.Send(new ResolvePublicLinkQuery(token, password), ct);

        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }

    /// <summary>
    /// Submits a password for a password-protected public link.
    /// Returns the link DTO on success so the client can proceed.
    /// </summary>
    [HttpPost("{token}/unlock")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> UnlockPublicLink(
        string token,
        [FromBody] UnlockPublicLinkRequest request,
        CancellationToken ct)
    {
        var result = await sender.Send(new ResolvePublicLinkQuery(token, request.Password), ct);

        return result.IsFailure ? result.ToProblemResult(this) : Ok(result.Value);
    }
}

public sealed record CreatePublicLinkRequest(
    Guid ResourceId,
    ShareResourceType ResourceType,
    ShareRole Role,
    DateTimeOffset? ExpiresAt,
    string? Password);

public sealed record UnlockPublicLinkRequest(string Password);
