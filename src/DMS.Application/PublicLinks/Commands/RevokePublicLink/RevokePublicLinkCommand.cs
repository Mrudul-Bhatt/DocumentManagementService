using DMS.Application.Common;
using MediatR;

namespace DMS.Application.PublicLinks.Commands.RevokePublicLink;

public sealed record RevokePublicLinkCommand(
    Guid LinkId,
    Guid RequestingUserId) : IRequest<Result>;
