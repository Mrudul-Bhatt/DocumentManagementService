using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Files.Queries.DownloadFile;

internal sealed class DownloadFileQueryHandler(
    IFileStorageService storageService,
    IFileMetadataRepository repository)
    : IRequestHandler<DownloadFileQuery, Result<FileDownloadResult>>
{
    public async Task<Result<FileDownloadResult>> Handle(DownloadFileQuery query, CancellationToken ct)
    {
        var metadata = await repository.GetByIdAsync(query.FileId, ct);

        if (metadata is null)
            return Result.Failure<FileDownloadResult>(DomainErrors.File.NotFound);

        if (!metadata.BelongsTo(query.UserId))
            return Result.Failure<FileDownloadResult>(DomainErrors.File.Forbidden);

        var stream = await storageService.ReadAsync(metadata.StoragePath, ct);

        return Result.Success(new FileDownloadResult(stream, metadata.Filename, metadata.MimeType));
    }
}
