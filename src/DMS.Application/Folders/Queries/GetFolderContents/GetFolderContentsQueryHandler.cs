using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Enums;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Folders.Queries.GetFolderContents;

internal sealed class GetFolderContentsQueryHandler(
    IFolderRepository folderRepository,
    IFileMetadataRepository fileRepository,
    IPermissionService permissionService)
    : IRequestHandler<GetFolderContentsQuery, Result<FolderContentsDto>>
{
    public async Task<Result<FolderContentsDto>> Handle(GetFolderContentsQuery query, CancellationToken ct)
    {
        if (query.FolderId.HasValue)
        {
            var folder = await folderRepository.GetByIdAsync(query.FolderId.Value, ct);

            if (folder is null)
                return Result.Failure<FolderContentsDto>(DomainErrors.Folder.NotFound);

            if (!folder.BelongsTo(query.UserId) &&
                !await permissionService.CanReadAsync(query.UserId, folder.Id, ShareResourceType.Folder, ct))
                return Result.Failure<FolderContentsDto>(DomainErrors.Folder.Forbidden);

            // Query content using the folder's actual owner so that shared access
            // returns the owner's files rather than the requesting user's files.
            var ownerId = folder.OwnerId;
            var subfolders = await folderRepository.GetChildrenAsync(query.FolderId.Value, ownerId, ct);
            var files      = await fileRepository.GetByFolderIdAsync(ownerId.ToString(), query.FolderId, ct);

            return Result.Success(new FolderContentsDto(
                subfolders.Select(f => new FolderDto(f.Id, f.Name, f.ParentFolderId, f.CreatedAt)).ToList().AsReadOnly(),
                files.Select(f => new FileMetadataDto(f.Id, f.Filename, f.FileSize, f.MimeType, f.UploadedAt, f.FolderId)).ToList().AsReadOnly()));
        }
        else
        {
            // Root view is always scoped to the requesting user's own content
            var rootFolders = await folderRepository.GetRootFoldersAsync(query.UserId, ct);
            var rootFiles   = await fileRepository.GetByFolderIdAsync(query.UserId.ToString(), null, ct);

            return Result.Success(new FolderContentsDto(
                rootFolders.Select(f => new FolderDto(f.Id, f.Name, f.ParentFolderId, f.CreatedAt)).ToList().AsReadOnly(),
                rootFiles.Select(f => new FileMetadataDto(f.Id, f.Filename, f.FileSize, f.MimeType, f.UploadedAt, f.FolderId)).ToList().AsReadOnly()));
        }
    }
}
