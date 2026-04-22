namespace DMS.Application.DTOs;

public sealed record FileMetadataDto(
    Guid Id,
    string Filename,
    long FileSize,
    string MimeType,
    DateTimeOffset UploadedAt);
