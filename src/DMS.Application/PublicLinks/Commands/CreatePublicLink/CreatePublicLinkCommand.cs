using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Enums;
using MediatR;

namespace DMS.Application.PublicLinks.Commands.CreatePublicLink;

public sealed record CreatePublicLinkCommand(
    Guid ResourceId,
    ShareResourceType ResourceType,
    ShareRole Role,
    Guid CreatedByUserId,
    DateTimeOffset? ExpiresAt,
    string? Password) : IRequest<Result<PublicLinkDto>>;
