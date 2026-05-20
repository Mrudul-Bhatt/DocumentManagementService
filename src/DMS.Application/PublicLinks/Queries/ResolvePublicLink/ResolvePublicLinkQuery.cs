using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.PublicLinks.Queries.ResolvePublicLink;

/// <summary>
/// Validates a public link token and optional password, returning the link details
/// if access is granted. Used by the unauthenticated public link endpoint.
/// </summary>
public sealed record ResolvePublicLinkQuery(
    string Token,
    string? Password) : IRequest<Result<PublicLinkDto>>;
