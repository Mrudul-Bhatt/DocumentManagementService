using DMS.Application.Common;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DMS.Application.Files.Commands.DeleteFile;

internal sealed class DeleteFileCommandHandler(
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

        metadata.SoftDelete();
        await repository.UpdateAsync(metadata, ct);

        logger.LogInformation("File {FileId} moved to trash by user {UserId}", command.FileId, command.UserId);

        return Result.Success();
    }
}
