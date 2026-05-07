using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;

namespace DMS.Application.Files.Commands.DeleteFileVersion;

internal sealed class DeleteFileVersionCommandHandler(
    IFileMetadataRepository fileRepository,
    IFileVersionRepository versionRepository,
    IFileStorageService storageService)
    : IRequestHandler<DeleteFileVersionCommand, Result>
{
    public async Task<Result> Handle(DeleteFileVersionCommand command, CancellationToken ct)
    {
        var file = await fileRepository.GetByIdAsync(command.FileId, ct);

        if (file is null)
            return Result.Failure(DomainErrors.File.NotFound);

        if (!file.BelongsTo(command.UserId.ToString()))
            return Result.Failure(DomainErrors.File.Forbidden);

        var version = await versionRepository.GetByIdAsync(command.VersionId, ct);

        if (version is null || version.FileId != command.FileId)
            return Result.Failure(DomainErrors.FileVersion.NotFound);

        // Prevent deleting the version that currently backs the live file
        if (version.StoragePath == file.StoragePath)
            return Result.Failure(DomainErrors.FileVersion.CannotDeleteCurrent);

        await storageService.DeleteAsync(version.StoragePath, ct);
        await versionRepository.DeleteAsync(version, ct);

        return Result.Success();
    }
}
