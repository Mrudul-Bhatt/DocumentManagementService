using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Folders.Commands.MoveFolder;

internal sealed class MoveFolderCommandHandler(
    IFolderRepository folderRepository)
    : IRequestHandler<MoveFolderCommand, Result>
{
    private const int MaxFolderDepth = 20;

    public async Task<Result> Handle(MoveFolderCommand command, CancellationToken ct)
    {
        var folder = await folderRepository.GetByIdAsync(command.FolderId, ct);

        if (folder is null || !folder.BelongsTo(command.UserId))
            return Result.Failure(DomainErrors.Folder.NotFound);

        if (command.TargetParentFolderId.HasValue)
        {
            // Prevent moving a folder into itself or one of its own descendants
            var descendantIds = await folderRepository.GetDescendantIdsAsync(command.FolderId, ct);
            if (command.TargetParentFolderId.Value == command.FolderId ||
                descendantIds.Contains(command.TargetParentFolderId.Value))
                return Result.Failure(DomainErrors.Folder.Forbidden);

            var target = await folderRepository.GetByIdAsync(command.TargetParentFolderId.Value, ct);

            if (target is null || !target.BelongsTo(command.UserId))
                return Result.Failure(DomainErrors.Folder.NotFound);

            var targetDepth = await folderRepository.GetDepthAsync(command.TargetParentFolderId.Value, ct);
            if (targetDepth + 1 >= MaxFolderDepth)
                return Result.Failure(DomainErrors.Folder.MaxDepthExceeded);

            var siblings = await folderRepository.GetChildrenAsync(command.TargetParentFolderId.Value, command.UserId, ct);
            if (siblings.Any(f => f.Name.Equals(folder.Name, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure(DomainErrors.Folder.NameConflict);
        }
        else
        {
            var roots = await folderRepository.GetRootFoldersAsync(command.UserId, ct);
            if (roots.Any(f => f.Id != folder.Id && f.Name.Equals(folder.Name, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure(DomainErrors.Folder.NameConflict);
        }

        folder.MoveTo(command.TargetParentFolderId);
        await folderRepository.UpdateAsync(folder, ct);

        return Result.Success();
    }
}
