using DMS.Application.Common;
using DMS.Domain.Enums;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Folders.Commands.MoveFolder;

internal sealed class MoveFolderCommandHandler(
    IFolderRepository folderRepository,
    IPermissionService permissionService)
    : IRequestHandler<MoveFolderCommand, Result>
{
    private const int MaxFolderDepth = 20;

    public async Task<Result> Handle(MoveFolderCommand command, CancellationToken ct)
    {
        var folder = await folderRepository.GetByIdAsync(command.FolderId, ct);

        if (folder is null)
            return Result.Failure(DomainErrors.Folder.NotFound);

        if (!folder.BelongsTo(command.UserId) &&
            !await permissionService.CanWriteAsync(command.UserId, folder.Id, ShareResourceType.Folder, ct))
            return Result.Failure(DomainErrors.Folder.Forbidden);

        if (command.TargetParentFolderId.HasValue)
        {
            var descendantIds = await folderRepository.GetDescendantIdsAsync(command.FolderId, ct);
            if (command.TargetParentFolderId.Value == command.FolderId ||
                descendantIds.Contains(command.TargetParentFolderId.Value))
                return Result.Failure(DomainErrors.Folder.Forbidden);

            var target = await folderRepository.GetByIdAsync(command.TargetParentFolderId.Value, ct);

            if (target is null)
                return Result.Failure(DomainErrors.Folder.NotFound);

            if (!target.BelongsTo(command.UserId) &&
                !await permissionService.CanWriteAsync(command.UserId, target.Id, ShareResourceType.Folder, ct))
                return Result.Failure(DomainErrors.Folder.Forbidden);

            var targetDepth = await folderRepository.GetDepthAsync(command.TargetParentFolderId.Value, ct);
            if (targetDepth + 1 >= MaxFolderDepth)
                return Result.Failure(DomainErrors.Folder.MaxDepthExceeded);

            var ownerId  = target.OwnerId;
            var siblings = await folderRepository.GetChildrenAsync(command.TargetParentFolderId.Value, ownerId, ct);
            if (siblings.Any(f => f.Name.Equals(folder.Name, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure(DomainErrors.Folder.NameConflict);
        }
        else
        {
            var roots = await folderRepository.GetRootFoldersAsync(folder.OwnerId, ct);
            if (roots.Any(f => f.Id != folder.Id && f.Name.Equals(folder.Name, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure(DomainErrors.Folder.NameConflict);
        }

        folder.MoveTo(command.TargetParentFolderId);
        await folderRepository.UpdateAsync(folder, ct);

        return Result.Success();
    }
}
