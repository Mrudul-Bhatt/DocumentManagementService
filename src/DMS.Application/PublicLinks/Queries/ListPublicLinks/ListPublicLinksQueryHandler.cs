using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.PublicLinks.Queries.ListPublicLinks;

internal sealed class ListPublicLinksQueryHandler(
    IPublicLinkRepository linkRepository,
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository) : IRequestHandler<ListPublicLinksQuery, Result<IReadOnlyList<PublicLinkDto>>>
{
    public async Task<Result<IReadOnlyList<PublicLinkDto>>> Handle(ListPublicLinksQuery query, CancellationToken ct)
    {
        // Only the resource owner may list its public links
        if (query.ResourceType == Domain.Enums.ShareResourceType.File)
        {
            var file = await fileRepository.GetByIdAsync(query.ResourceId, ct);
            if (file is null || !file.BelongsTo(query.RequestingUserId.ToString()))
                return Result.Failure<IReadOnlyList<PublicLinkDto>>(DomainErrors.File.NotFound);
        }
        else
        {
            var folder = await folderRepository.GetByIdAsync(query.ResourceId, ct);
            if (folder is null || !folder.BelongsTo(query.RequestingUserId))
                return Result.Failure<IReadOnlyList<PublicLinkDto>>(DomainErrors.Folder.NotFound);
        }

        var links = await linkRepository.GetByResourceAsync(query.ResourceId, query.ResourceType, ct);

        IReadOnlyList<PublicLinkDto> dtos = links
            .Select(l => new PublicLinkDto(l.Id, l.ResourceId, l.ResourceType, l.Token, l.Role, l.ExpiresAt, l.RequiresPassword(), l.CreatedAt))
            .ToList()
            .AsReadOnly();

        return Result.Success(dtos);
    }
}
