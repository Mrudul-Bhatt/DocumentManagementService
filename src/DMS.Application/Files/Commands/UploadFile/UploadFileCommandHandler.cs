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
    IFileMetadataRepository fileRepository,
    IFolderRepository folderRepository,
    IFileVersionRepository versionRepository,
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

        // Validate folder ownership when a folder is specified
        if (command.FolderId.HasValue)
        {
            var folder = await folderRepository.GetByIdAsync(command.FolderId.Value, ct);
            if (folder is null || !folder.BelongsTo(Guid.Parse(command.UserId)))
                return Result.Failure<FileMetadataDto>(DomainErrors.Folder.NotFound);
        }

        // Check whether a file with the same name already exists in the target folder
        var existingFiles = await fileRepository.GetByFolderIdAsync(command.UserId, command.FolderId, ct);
        var existing = existingFiles.FirstOrDefault(f =>
            string.Equals(f.Filename, command.Filename, StringComparison.OrdinalIgnoreCase));

        var fileId = Guid.NewGuid().ToString();
        var storagePath = await storageService.SaveAsync(command.Content, command.UserId, fileId, ct);

        if (existing is not null)
        {
            // Same-named file in the same folder → create a new version rather than a duplicate
            var nextVersion = await versionRepository.GetLatestVersionNumberAsync(existing.Id, ct) + 1;

            var version = FileVersion.Create(
                existing.Id,
                nextVersion,
                storagePath,
                command.FileSize,
                command.UserId);

            await versionRepository.AddAsync(version, ct);

            existing.UpdateStoragePath(storagePath);
            await fileRepository.UpdateAsync(existing, ct);

            logger.LogInformation(
                "File {FileId} updated to version {Version} by user {UserId} ({Filename}, {Size} bytes)",
                existing.Id, nextVersion, command.UserId, command.Filename, command.FileSize);

            return Result.Success(new FileMetadataDto(
                existing.Id,
                existing.Filename,
                command.FileSize,
                existing.MimeType,
                existing.UploadedAt,
                existing.FolderId));
        }

        // New file — create metadata and version 1
        var metadata = FileMetadata.Create(
            command.UserId,
            command.Filename,
            command.FileSize,
            command.MimeType,
            storagePath,
            command.FolderId);

        await fileRepository.AddAsync(metadata, ct);

        var firstVersion = FileVersion.Create(
            metadata.Id,
            versionNumber: 1,
            storagePath,
            command.FileSize,
            command.UserId);

        await versionRepository.AddAsync(firstVersion, ct);

        logger.LogInformation(
            "File {FileId} uploaded by user {UserId} ({Filename}, {Size} bytes)",
            metadata.Id, command.UserId, command.Filename, command.FileSize);

        return Result.Success(new FileMetadataDto(
            metadata.Id,
            metadata.Filename,
            metadata.FileSize,
            metadata.MimeType,
            metadata.UploadedAt,
            metadata.FolderId));
    }
}
