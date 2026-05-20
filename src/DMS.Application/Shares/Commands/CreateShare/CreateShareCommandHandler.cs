using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Application.Services;
using DMS.Domain.Entities;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Shares.Commands.CreateShare;

internal sealed class CreateShareCommandHandler(
    IShareRepository shareRepository,
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository,
    IUserRepository userRepository,
    IPermissionService permissionService,
    IEmailService emailService) : IRequestHandler<CreateShareCommand, Result<ShareDto>>
{
    private const int MaxSharesPerResource = 500;

    public async Task<Result<ShareDto>> Handle(CreateShareCommand command, CancellationToken ct)
    {
        // Resolve the invitee's email to a user ID
        var grantee = await userRepository.GetByEmailAsync(command.GrantedToEmail, ct);
        if (grantee is null)
            return Result.Failure<ShareDto>(DomainErrors.User.NotFound);

        if (grantee.Id == command.GrantedByUserId)
            return Result.Failure<ShareDto>(DomainErrors.Share.CannotShareWithSelf);

        // Verify the caller owns the resource being shared
        string resourceName;
        if (command.ResourceType == Domain.Enums.ShareResourceType.File)
        {
            var file = await fileRepository.GetByIdAsync(command.ResourceId, ct);
            if (file is null || !file.BelongsTo(command.GrantedByUserId.ToString()))
                return Result.Failure<ShareDto>(DomainErrors.File.NotFound);
            resourceName = file.Filename;
        }
        else
        {
            var folder = await folderRepository.GetByIdAsync(command.ResourceId, ct);
            if (folder is null || !folder.BelongsTo(command.GrantedByUserId))
                return Result.Failure<ShareDto>(DomainErrors.Folder.NotFound);
            resourceName = folder.Name;
        }

        // Enforce the 500-principal cap
        var shareCount = await shareRepository.CountByResourceAsync(command.ResourceId, command.ResourceType, ct);
        if (shareCount >= MaxSharesPerResource)
            return Result.Failure<ShareDto>(DomainErrors.Share.MaxSharesExceeded);

        // Prevent duplicate grants
        var existing = await shareRepository.GetDirectShareAsync(command.ResourceId, command.ResourceType, grantee.Id, ct);
        if (existing is not null)
            return Result.Failure<ShareDto>(DomainErrors.Share.AlreadyExists);

        var share = Share.Create(command.ResourceId, command.ResourceType, grantee.Id, command.GrantedByUserId, command.Role);
        await shareRepository.AddAsync(share, ct);

        // Invalidate cached permissions for this resource so the grantee's next request reflects the new grant
        await permissionService.InvalidateCacheAsync(command.ResourceId, command.ResourceType, ct);

        // Resolve the granter's email for the notification
        var granter = await userRepository.GetByIdAsync(command.GrantedByUserId, ct);
        var granterEmail = granter?.Email ?? "someone";
        await emailService.SendShareInvitationAsync(grantee.Email, granterEmail, resourceName, command.Role.ToString(), ct);

        return Result.Success(new ShareDto(
            share.Id,
            share.ResourceId,
            share.ResourceType,
            share.GrantedToUserId,
            grantee.Email,
            share.Role,
            share.CreatedAt));
    }
}
