using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Shares.Queries.ListSharedWithMe;

internal sealed class ListSharedWithMeQueryHandler(
    IShareRepository shareRepository,
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository) : IRequestHandler<ListSharedWithMeQuery, Result<IReadOnlyList<SharedResourceDto>>>
{
    public async Task<Result<IReadOnlyList<SharedResourceDto>>> Handle(ListSharedWithMeQuery query, CancellationToken ct)
    {
        var shares = await shareRepository.GetForUserAsync(query.UserId, ct);

        var dtos = new List<SharedResourceDto>(shares.Count);
        foreach (var share in shares)
        {
            string name;
            if (share.ResourceType == Domain.Enums.ShareResourceType.File)
            {
                var file = await fileRepository.GetByIdAsync(share.ResourceId, ct);
                name = file?.Filename ?? share.ResourceId.ToString();
            }
            else
            {
                var folder = await folderRepository.GetByIdAsync(share.ResourceId, ct);
                name = folder?.Name ?? share.ResourceId.ToString();
            }

            dtos.Add(new SharedResourceDto(
                share.ResourceId,
                name,
                share.ResourceType,
                share.Role,
                share.GrantedByUserId,
                share.CreatedAt));
        }

        return Result.Success<IReadOnlyList<SharedResourceDto>>(dtos.AsReadOnly());
    }
}
