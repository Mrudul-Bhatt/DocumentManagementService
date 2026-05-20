using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Shares.Commands.RevokeShare;

internal sealed class RevokeShareCommandHandler(
    IShareRepository shareRepository,
    IPermissionService permissionService) : IRequestHandler<RevokeShareCommand, Result>
{
    public async Task<Result> Handle(RevokeShareCommand command, CancellationToken ct)
    {
        var share = await shareRepository.GetByIdAsync(command.ShareId, ct);

        if (share is null)
            return Result.Failure(DomainErrors.Share.NotFound);

        // Only the owner who created the share or the grantee themselves may revoke it
        var isGranter  = share.GrantedByUserId == command.RequestingUserId;
        var isGrantee  = share.IsGrantedTo(command.RequestingUserId);
        if (!isGranter && !isGrantee)
            return Result.Failure(DomainErrors.Share.NotFound);

        await shareRepository.DeleteAsync(share, ct);

        await permissionService.InvalidateCacheAsync(share.ResourceId, share.ResourceType, ct);

        return Result.Success();
    }
}
