using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Folders.Commands.DeleteFolder;

internal sealed class DeleteFolderCommandHandler(
    IFolderRepository folderRepository,
    IFileMetadataRepository fileRepository)
    : IRequestHandler<DeleteFolderCommand, Result>
{
    public async Task<Result> Handle(DeleteFolderCommand command, CancellationToken ct)
    {
        var folder = await folderRepository.GetByIdAsync(command.FolderId, ct);

        if (folder is null || !folder.BelongsTo(command.UserId))
            return Result.Failure(DomainErrors.Folder.NotFound);

        // Collect the folder itself plus all descendants
        var descendantIds = await folderRepository.GetDescendantIdsAsync(command.FolderId, ct);
        var allFolderIds = descendantIds.Append(command.FolderId).ToList();

        // Soft-delete all files in all affected folders
        foreach (var folderId in allFolderIds)
        {
            var files = await fileRepository.GetByFolderIdAsync(command.UserId.ToString(), folderId, ct);
            foreach (var file in files)
            {
                file.SoftDelete();
                await fileRepository.UpdateAsync(file, ct);
            }
        }

        // Soft-delete all descendant folders, then the folder itself
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
