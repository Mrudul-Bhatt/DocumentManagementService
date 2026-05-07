using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Folders.Commands.RestoreFolder;

internal sealed class RestoreFolderCommandHandler(
    IFolderRepository folderRepository)
    : IRequestHandler<RestoreFolderCommand, Result>
{
    public async Task<Result> Handle(RestoreFolderCommand command, CancellationToken ct)
    {
        var folder = await folderRepository.GetDeletedByIdAsync(command.FolderId, ct);

        if (folder is null || !folder.BelongsTo(command.UserId))
            return Result.Failure(DomainErrors.Folder.NotFound);

        folder.Restore();
        await folderRepository.UpdateAsync(folder, ct);

        return Result.Success();
    }
}
