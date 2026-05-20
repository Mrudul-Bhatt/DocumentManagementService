using DMS.Application.Common;
using DMS.Domain.Enums;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Folders.Commands.RenameFolder;

internal sealed class RenameFolderCommandHandler(
    IFolderRepository folderRepository,
    IPermissionService permissionService)
    : IRequestHandler<RenameFolderCommand, Result>
{
    public async Task<Result> Handle(RenameFolderCommand command, CancellationToken ct)
    {
        var folder = await folderRepository.GetByIdAsync(command.FolderId, ct);

        if (folder is null)
            return Result.Failure(DomainErrors.Folder.NotFound);

        if (!folder.BelongsTo(command.UserId) &&
            !await permissionService.CanWriteAsync(command.UserId, folder.Id, ShareResourceType.Folder, ct))
            return Result.Failure(DomainErrors.Folder.Forbidden);

        var ownerId = folder.OwnerId;
        IReadOnlyList<Domain.Entities.Folder> siblings = folder.ParentFolderId.HasValue
            ? await folderRepository.GetChildrenAsync(folder.ParentFolderId.Value, ownerId, ct)
            : await folderRepository.GetRootFoldersAsync(ownerId, ct);

        if (siblings.Any(f => f.Id != folder.Id && f.Name.Equals(command.NewName, StringComparison.OrdinalIgnoreCase)))
            return Result.Failure(DomainErrors.Folder.NameConflict);

        folder.Rename(command.NewName);
        await folderRepository.UpdateAsync(folder, ct);

        return Result.Success();
    }
}
