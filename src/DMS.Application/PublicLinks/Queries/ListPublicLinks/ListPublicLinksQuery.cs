using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Enums;
using MediatR;

namespace DMS.Application.PublicLinks.Queries.ListPublicLinks;

public sealed record ListPublicLinksQuery(
    Guid ResourceId,
    ShareResourceType ResourceType,
    Guid RequestingUserId) : IRequest<Result<IReadOnlyList<PublicLinkDto>>>;
