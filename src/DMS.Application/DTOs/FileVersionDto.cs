namespace DMS.Application.DTOs;

public sealed record FileVersionDto(
    Guid Id,
    int VersionNumber,
    long FileSize,
    string UploadedBy,
    DateTimeOffset CreatedAt);
