using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Folders.Queries.GetFolderContents;

internal sealed class GetFolderContentsQueryHandler(
    IFolderRepository folderRepository,
    IFileMetadataRepository fileRepository)
    : IRequestHandler<GetFolderContentsQuery, Result<FolderContentsDto>>
{
    public async Task<Result<FolderContentsDto>> Handle(GetFolderContentsQuery query, CancellationToken ct)
    {
        if (query.FolderId.HasValue)
        {
            var folder = await folderRepository.GetByIdAsync(query.FolderId.Value, ct);
            if (folder is null || !folder.BelongsTo(query.UserId))
                return Result.Failure<FolderContentsDto>(DomainErrors.Folder.NotFound);

            var subfolders = await folderRepository.GetChildrenAsync(query.FolderId.Value, query.UserId, ct);
            var files      = await fileRepository.GetByFolderIdAsync(query.UserId.ToString(), query.FolderId, ct);

            return Result.Success(new FolderContentsDto(
                subfolders.Select(f => new FolderDto(f.Id, f.Name, f.ParentFolderId, f.CreatedAt)).ToList().AsReadOnly(),
                files.Select(f => new FileMetadataDto(f.Id, f.Filename, f.FileSize, f.MimeType, f.UploadedAt, f.FolderId)).ToList().AsReadOnly()));
        }
        else
        {
            var rootFolders = await folderRepository.GetRootFoldersAsync(query.UserId, ct);
            var rootFiles   = await fileRepository.GetByFolderIdAsync(query.UserId.ToString(), null, ct);

            return Result.Success(new FolderContentsDto(
                rootFolders.Select(f => new FolderDto(f.Id, f.Name, f.ParentFolderId, f.CreatedAt)).ToList().AsReadOnly(),
                rootFiles.Select(f => new FileMetadataDto(f.Id, f.Filename, f.FileSize, f.MimeType, f.UploadedAt, f.FolderId)).ToList().AsReadOnly()));
        }
    }
}
