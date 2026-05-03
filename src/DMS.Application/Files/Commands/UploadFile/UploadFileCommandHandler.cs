using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Entities;
using DMS.Domain.Errors;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DMS.Application.Files.Commands.UploadFile;

/// <summary>
/// Handles the UploadFileCommand: validates the upload, persists bytes to storage,
/// saves metadata to the database, and returns a DTO representing the new file.
///
/// Why internal sealed?
///   internal: the handler is an implementation detail of the Application layer.
///   Nothing outside this assembly should reference it directly — all interaction
///   goes through MediatR dispatch (sender.Send(command)). Keeping it internal
///   enforces that boundary at the compiler level.
///   sealed: handlers have no meaningful base behaviour to inherit or override.
///
/// Dependency breakdown:
///   IFileStorageService — writes bytes to disk (Infrastructure concern, injected via interface).
///   IFileMetadataRepository — persists metadata to the database (Infrastructure concern).
///   ILogger — structured logging; injected so log output can be captured in tests.
///
/// Why inject interfaces and not concrete types?
///   The Application layer must not reference Infrastructure assemblies — that would
///   invert the dependency direction and couple business logic to infrastructure
///   details (file paths, SQL, etc.). Interfaces defined in Domain/Application allow
///   Infrastructure to implement them without the dependency flowing the wrong way.
/// </summary>
internal sealed class UploadFileCommandHandler(
    IFileStorageService storageService,
    IFileMetadataRepository repository,
    ILogger<UploadFileCommandHandler> logger)
    : IRequestHandler<UploadFileCommand, Result<FileMetadataDto>>
{
    // Duplicated from [RequestSizeLimit] on the controller as a defence-in-depth check.
    // The controller attribute rejects the request at the ASP.NET Core pipeline level
    // before the body is read. This constant guards against the handler being called
    // from any path that bypasses the controller (e.g., tests, future CLI tooling).
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    /// <summary>
    /// Executes the upload operation.
    ///
    /// Flow:
    ///   1. Validate file is not empty and does not exceed the size limit.
    ///   2. Generate a unique storage key and persist bytes via IFileStorageService.
    ///   3. Create a FileMetadata entity via its factory method and persist to the DB.
    ///   4. Log the upload event for observability.
    ///   5. Project the saved entity into a FileMetadataDto and return it wrapped in Result.
    ///
    /// Why validate before calling SaveAsync?
    ///   Fail fast: reject invalid input before touching the filesystem or database.
    ///   A 0-byte or oversized file would waste I/O and leave orphaned storage files
    ///   if the subsequent DB write failed.
    ///
    /// Why generate fileId here rather than using metadata.Id?
    ///   The storage path is needed as an argument to FileMetadata.Create(), so it must
    ///   be resolved before the entity is created. A separate fileId (Guid string) is
    ///   generated to name the file on disk, decoupling the storage key from the DB
    ///   primary key (metadata.Id is generated inside the entity factory).
    /// </summary>
    public async Task<Result<FileMetadataDto>> Handle(UploadFileCommand command, CancellationToken ct)
    {
        if (command.FileSize == 0)
            return Result.Failure<FileMetadataDto>(DomainErrors.File.Empty);

        if (command.FileSize > MaxFileSizeBytes)
            return Result.Failure<FileMetadataDto>(DomainErrors.File.TooLarge);

        // Generate a unique key for the file on disk.
        // Using a GUID string rather than the original filename avoids collisions
        // and prevents path traversal attacks (e.g., filenames containing "../").
        var fileId = Guid.NewGuid().ToString();
        var storagePath = await storageService.SaveAsync(command.Content, command.UserId, fileId, ct);

        // FileMetadata.Create() is the entity's factory method — it encapsulates
        // the domain rules for constructing a valid entity (generates Id, sets UploadedAt).
        // Calling new FileMetadata(...) directly is not possible because the constructor
        // is private, enforcing that all entities are created through validated paths.
        var metadata = FileMetadata.Create(
            command.UserId,
            command.Filename,
            command.FileSize,
            command.MimeType,
            storagePath);

        await repository.AddAsync(metadata, ct);

        // Structured log: {FileId}, {UserId}, etc. are named properties, not string
        // interpolation placeholders. Log aggregators index them as queryable fields.
        logger.LogInformation("File {FileId} uploaded by user {UserId} ({Filename}, {Size} bytes)",
            metadata.Id, command.UserId, command.Filename, command.FileSize);

        // Project the persisted entity to a DTO — the API layer never receives the
        // raw domain entity, only this flat read-only snapshot.
        return Result.Success(new FileMetadataDto(
            metadata.Id,
            metadata.Filename,
            metadata.FileSize,
            metadata.MimeType,
            metadata.UploadedAt));
    }
}
