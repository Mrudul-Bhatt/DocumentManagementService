namespace DMS.Application.DTOs;

public sealed record FolderDto(
    Guid Id,
    string Name,
    Guid? ParentFolderId,
    DateTimeOffset CreatedAt);
