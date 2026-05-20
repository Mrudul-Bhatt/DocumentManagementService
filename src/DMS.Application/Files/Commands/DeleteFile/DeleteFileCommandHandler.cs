using DMS.Application.Common;
using DMS.Domain.Enums;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DMS.Application.Files.Commands.DeleteFile;

internal sealed class DeleteFileCommandHandler(
    IFileMetadataRepository repository,
    IPermissionService permissionService,
    ILogger<DeleteFileCommandHandler> logger)
    : IRequestHandler<DeleteFileCommand, Result>
{
    public async Task<Result> Handle(DeleteFileCommand command, CancellationToken ct)
    {
        var metadata = await repository.GetByIdAsync(command.FileId, ct);

        if (metadata is null)
            return Result.Failure(DomainErrors.File.NotFound);

        if (!metadata.BelongsTo(command.UserId) &&
            !await permissionService.CanWriteAsync(Guid.Parse(command.UserId), metadata.Id, ShareResourceType.File, ct))
            return Result.Failure(DomainErrors.File.Forbidden);

        metadata.SoftDelete();
        await repository.UpdateAsync(metadata, ct);

        logger.LogInformation("File {FileId} moved to trash by user {UserId}", command.FileId, command.UserId);

        return Result.Success();
    }
}
