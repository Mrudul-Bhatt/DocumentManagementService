using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Trash.Commands.RestoreFromTrash;

internal sealed class RestoreFromTrashCommandHandler(
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository)
    : IRequestHandler<RestoreFromTrashCommand, Result>
{
    public async Task<Result> Handle(RestoreFromTrashCommand command, CancellationToken ct)
    {
        if (command.ItemType == TrashedItemType.File)
        {
            var file = await fileRepository.GetDeletedByIdAsync(command.ItemId, ct);

            if (file is null || !file.BelongsTo(command.UserId.ToString()))
                return Result.Failure(DomainErrors.File.NotFound);

            file.Restore();
            await fileRepository.UpdateAsync(file, ct);
        }
        else
        {
            var folder = await folderRepository.GetDeletedByIdAsync(command.ItemId, ct);

            if (folder is null || !folder.BelongsTo(command.UserId))
                return Result.Failure(DomainErrors.Folder.NotFound);

            folder.Restore();
            await folderRepository.UpdateAsync(folder, ct);
        }

        return Result.Success();
    }
}
