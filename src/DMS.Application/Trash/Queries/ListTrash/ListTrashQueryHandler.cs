using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Trash.Queries.ListTrash;

internal sealed class ListTrashQueryHandler(
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository)
    : IRequestHandler<ListTrashQuery, Result<IReadOnlyList<TrashedItemDto>>>
{
    public async Task<Result<IReadOnlyList<TrashedItemDto>>> Handle(ListTrashQuery query, CancellationToken ct)
    {
        var deletedFiles   = await fileRepository.GetDeletedByUserIdAsync(query.UserId.ToString(), ct);
        var deletedFolders = await folderRepository.GetDeletedByUserIdAsync(query.UserId, ct);

        var items = new List<TrashedItemDto>(deletedFiles.Count + deletedFolders.Count);

        items.AddRange(deletedFiles.Select(f =>
            new TrashedItemDto(f.Id, f.Filename, TrashedItemType.File, f.DeletedAt!.Value)));

        items.AddRange(deletedFolders.Select(f =>
            new TrashedItemDto(f.Id, f.Name, TrashedItemType.Folder, f.DeletedAt!.Value)));

        IReadOnlyList<TrashedItemDto> result = items
            .OrderByDescending(i => i.DeletedAt)
            .ToList()
            .AsReadOnly();

        return Result.Success(result);
    }
}
