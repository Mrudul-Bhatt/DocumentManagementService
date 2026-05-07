using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Files.Commands.RestoreFileVersion;

internal sealed class RestoreFileVersionCommandHandler(
    IFileMetadataRepository fileRepository,
    IFileVersionRepository versionRepository)
    : IRequestHandler<RestoreFileVersionCommand, Result>
{
    public async Task<Result> Handle(RestoreFileVersionCommand command, CancellationToken ct)
    {
        var file = await fileRepository.GetByIdAsync(command.FileId, ct);

        if (file is null)
            return Result.Failure(DomainErrors.File.NotFound);

        if (!file.BelongsTo(command.UserId.ToString()))
            return Result.Failure(DomainErrors.File.Forbidden);

        var version = await versionRepository.GetByIdAsync(command.VersionId, ct);

        if (version is null || version.FileId != command.FileId)
            return Result.Failure(DomainErrors.FileVersion.NotFound);

        // Point the live file at the chosen version's physical storage path
        file.UpdateStoragePath(version.StoragePath);
        await fileRepository.UpdateAsync(file, ct);

        return Result.Success();
    }
}
