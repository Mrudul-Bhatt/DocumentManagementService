using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DMS.Application.Files.Commands.DeleteFile;

internal sealed class DeleteFileCommandHandler(
    IFileStorageService storageService,
    IFileMetadataRepository repository,
    ILogger<DeleteFileCommandHandler> logger)
    : IRequestHandler<DeleteFileCommand, Result>
{
    public async Task<Result> Handle(DeleteFileCommand command, CancellationToken ct)
    {
        var metadata = await repository.GetByIdAsync(command.FileId, ct);

        if (metadata is null)
            return Result.Failure(DomainErrors.File.NotFound);

        if (!metadata.BelongsTo(command.UserId))
            return Result.Failure(DomainErrors.File.Forbidden);

        await storageService.DeleteAsync(metadata.StoragePath, ct);
        await repository.DeleteAsync(metadata, ct);

        logger.LogInformation("File {FileId} deleted by user {UserId}", command.FileId, command.UserId);

        return Result.Success();
    }
}
