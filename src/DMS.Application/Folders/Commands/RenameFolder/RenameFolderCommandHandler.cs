using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Folders.Commands.RenameFolder;

internal sealed class RenameFolderCommandHandler(
    IFolderRepository folderRepository)
    : IRequestHandler<RenameFolderCommand, Result>
{
    public async Task<Result> Handle(RenameFolderCommand command, CancellationToken ct)
    {
        var folder = await folderRepository.GetByIdAsync(command.FolderId, ct);

        if (folder is null || !folder.BelongsTo(command.UserId))
            return Result.Failure(DomainErrors.Folder.NotFound);

        // Check for name conflict among siblings
        IReadOnlyList<Domain.Entities.Folder> siblings = folder.ParentFolderId.HasValue
            ? await folderRepository.GetChildrenAsync(folder.ParentFolderId.Value, command.UserId, ct)
            : await folderRepository.GetRootFoldersAsync(command.UserId, ct);

        if (siblings.Any(f => f.Id != folder.Id && f.Name.Equals(command.NewName, StringComparison.OrdinalIgnoreCase)))
            return Result.Failure(DomainErrors.Folder.NameConflict);

        folder.Rename(command.NewName);
        await folderRepository.UpdateAsync(folder, ct);

        return Result.Success();
    }
}
