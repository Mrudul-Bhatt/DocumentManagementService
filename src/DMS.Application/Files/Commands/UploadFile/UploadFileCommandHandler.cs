using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Entities;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DMS.Application.Files.Commands.UploadFile;

internal sealed class UploadFileCommandHandler(
    IFileStorageService storageService,
    IFileMetadataRepository repository,
    ILogger<UploadFileCommandHandler> logger)
    : IRequestHandler<UploadFileCommand, Result<FileMetadataDto>>
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    public async Task<Result<FileMetadataDto>> Handle(UploadFileCommand command, CancellationToken ct)
    {
        if (command.FileSize == 0)
            return Result.Failure<FileMetadataDto>(DomainErrors.File.Empty);

        if (command.FileSize > MaxFileSizeBytes)
            return Result.Failure<FileMetadataDto>(DomainErrors.File.TooLarge);

        var fileId = Guid.NewGuid().ToString();
        var storagePath = await storageService.SaveAsync(command.Content, command.UserId, fileId, ct);

        var metadata = FileMetadata.Create(
            command.UserId,
            command.Filename,
            command.FileSize,
            command.MimeType,
            storagePath);

        await repository.AddAsync(metadata, ct);

        logger.LogInformation("File {FileId} uploaded by user {UserId} ({Filename}, {Size} bytes)",
            metadata.Id, command.UserId, command.Filename, command.FileSize);

        return Result.Success(new FileMetadataDto(
            metadata.Id,
            metadata.Filename,
            metadata.FileSize,
            metadata.MimeType,
            metadata.UploadedAt));
    }
}
