using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.PublicLinks.Commands.RevokePublicLink;

internal sealed class RevokePublicLinkCommandHandler(
    IPublicLinkRepository linkRepository) : IRequestHandler<RevokePublicLinkCommand, Result>
{
    public async Task<Result> Handle(RevokePublicLinkCommand command, CancellationToken ct)
    {
        var link = await linkRepository.GetByIdAsync(command.LinkId, ct);

        if (link is null || link.CreatedByUserId != command.RequestingUserId)
            return Result.Failure(DomainErrors.PublicLink.NotFound);

        await linkRepository.DeleteAsync(link, ct);

        return Result.Success();
    }
}
