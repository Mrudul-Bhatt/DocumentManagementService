using DMS.Application.Common;
using DMS.Domain.Enums;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Folders.Commands.DeleteFolder;

internal sealed class DeleteFolderCommandHandler(
    IFolderRepository folderRepository,
    IFileMetadataRepository fileRepository,
    IPermissionService permissionService)
    : IRequestHandler<DeleteFolderCommand, Result>
{
    public async Task<Result> Handle(DeleteFolderCommand command, CancellationToken ct)
    {
        var folder = await folderRepository.GetByIdAsync(command.FolderId, ct);

        if (folder is null)
            return Result.Failure(DomainErrors.Folder.NotFound);

        if (!folder.BelongsTo(command.UserId) &&
            !await permissionService.CanWriteAsync(command.UserId, folder.Id, ShareResourceType.Folder, ct))
            return Result.Failure(DomainErrors.Folder.Forbidden);

        var descendantIds = await folderRepository.GetDescendantIdsAsync(command.FolderId, ct);
        var allFolderIds  = descendantIds.Append(command.FolderId).ToList();

        foreach (var folderId in allFolderIds)
        {
            var files = await fileRepository.GetByFolderIdAsync(folder.OwnerId.ToString(), folderId, ct);
            foreach (var file in files)
            {
                file.SoftDelete();
                await fileRepository.UpdateAsync(file, ct);
            }
        }

        foreach (var descendantId in descendantIds)
        {
            var descendant = await folderRepository.GetByIdAsync(descendantId, ct);
            if (descendant is null) continue;
            descendant.SoftDelete();
            await folderRepository.UpdateAsync(descendant, ct);
        }

        folder.SoftDelete();
        await folderRepository.UpdateAsync(folder, ct);

        return Result.Success();
    }
}
