using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Entities;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Folders.Commands.CreateFolder;

internal sealed class CreateFolderCommandHandler(
    IFolderRepository folderRepository)
    : IRequestHandler<CreateFolderCommand, Result<FolderDto>>
{
    private const int MaxFolderDepth = 20;

    public async Task<Result<FolderDto>> Handle(CreateFolderCommand command, CancellationToken ct)
    {
        if (command.ParentFolderId.HasValue)
        {
            var parent = await folderRepository.GetByIdAsync(command.ParentFolderId.Value, ct);

            if (parent is null || !parent.BelongsTo(command.UserId))
                return Result.Failure<FolderDto>(DomainErrors.Folder.NotFound);

            var depth = await folderRepository.GetDepthAsync(command.ParentFolderId.Value, ct);
            if (depth >= MaxFolderDepth)
                return Result.Failure<FolderDto>(DomainErrors.Folder.MaxDepthExceeded);

            var siblings = await folderRepository.GetChildrenAsync(command.ParentFolderId.Value, command.UserId, ct);
            if (siblings.Any(f => f.Name.Equals(command.Name, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<FolderDto>(DomainErrors.Folder.NameConflict);
        }
        else
        {
            var roots = await folderRepository.GetRootFoldersAsync(command.UserId, ct);
            if (roots.Any(f => f.Name.Equals(command.Name, StringComparison.OrdinalIgnoreCase)))
                return Result.Failure<FolderDto>(DomainErrors.Folder.NameConflict);
        }

        var folder = Folder.Create(command.Name, command.UserId, command.ParentFolderId);
        await folderRepository.AddAsync(folder, ct);

        return Result.Success(new FolderDto(folder.Id, folder.Name, folder.ParentFolderId, folder.CreatedAt));
    }
}
