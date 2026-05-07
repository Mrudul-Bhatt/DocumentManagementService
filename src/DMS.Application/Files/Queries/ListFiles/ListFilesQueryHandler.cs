using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Files.Queries.ListFiles;

internal sealed class ListFilesQueryHandler(IFileMetadataRepository repository)
    : IRequestHandler<ListFilesQuery, Result<IReadOnlyList<FileMetadataDto>>>
{
    public async Task<Result<IReadOnlyList<FileMetadataDto>>> Handle(ListFilesQuery query, CancellationToken ct)
    {
        var files = await repository.GetByUserIdAsync(query.UserId, ct);

        var dtos = files
            .Select(f => new FileMetadataDto(f.Id, f.Filename, f.FileSize, f.MimeType, f.UploadedAt, f.FolderId))
            .ToList()
            .AsReadOnly();

        return Result.Success<IReadOnlyList<FileMetadataDto>>(dtos);
    }
}
