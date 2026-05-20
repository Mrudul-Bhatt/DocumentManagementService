using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Shares.Queries.ListShares;

internal sealed class ListSharesQueryHandler(
    IShareRepository shareRepository,
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository,
    IUserRepository userRepository) : IRequestHandler<ListSharesQuery, Result<IReadOnlyList<ShareDto>>>
{
    public async Task<Result<IReadOnlyList<ShareDto>>> Handle(ListSharesQuery query, CancellationToken ct)
    {
        // Only the resource owner may list its shares
        if (query.ResourceType == Domain.Enums.ShareResourceType.File)
        {
            var file = await fileRepository.GetByIdAsync(query.ResourceId, ct);
            if (file is null || !file.BelongsTo(query.RequestingUserId.ToString()))
                return Result.Failure<IReadOnlyList<ShareDto>>(DomainErrors.File.NotFound);
        }
        else
        {
            var folder = await folderRepository.GetByIdAsync(query.ResourceId, ct);
            if (folder is null || !folder.BelongsTo(query.RequestingUserId))
                return Result.Failure<IReadOnlyList<ShareDto>>(DomainErrors.Folder.NotFound);
        }

        var shares = await shareRepository.GetByResourceAsync(query.ResourceId, query.ResourceType, ct);

        var dtos = new List<ShareDto>(shares.Count);
        foreach (var share in shares)
        {
            var grantee = await userRepository.GetByIdAsync(share.GrantedToUserId, ct);
            dtos.Add(new ShareDto(
                share.Id,
                share.ResourceId,
                share.ResourceType,
                share.GrantedToUserId,
                grantee?.Email ?? share.GrantedToUserId.ToString(),
                share.Role,
                share.CreatedAt));
        }

        return Result.Success<IReadOnlyList<ShareDto>>(dtos.AsReadOnly());
    }
}
