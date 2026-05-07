using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Files.Queries.ListFileVersions;

internal sealed class ListFileVersionsQueryHandler(
    IFileMetadataRepository fileRepository,
    IFileVersionRepository versionRepository)
    : IRequestHandler<ListFileVersionsQuery, Result<IReadOnlyList<FileVersionDto>>>
{
    public async Task<Result<IReadOnlyList<FileVersionDto>>> Handle(ListFileVersionsQuery query, CancellationToken ct)
    {
        var file = await fileRepository.GetByIdAsync(query.FileId, ct);

        if (file is null)
            return Result.Failure<IReadOnlyList<FileVersionDto>>(DomainErrors.File.NotFound);

        if (!file.BelongsTo(query.UserId.ToString()))
            return Result.Failure<IReadOnlyList<FileVersionDto>>(DomainErrors.File.Forbidden);

        var versions = await versionRepository.GetByFileIdAsync(query.FileId, ct);

        IReadOnlyList<FileVersionDto> dtos = versions
            .Select(v => new FileVersionDto(v.Id, v.VersionNumber, v.FileSize, v.UploadedBy, v.CreatedAt))
            .ToList()
            .AsReadOnly();

        return Result.Success(dtos);
    }
}
