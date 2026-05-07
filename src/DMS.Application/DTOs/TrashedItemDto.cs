namespace DMS.Application.DTOs;

public enum TrashedItemType { File, Folder }

public sealed record TrashedItemDto(
    Guid Id,
    string Name,
    TrashedItemType Type,
    DateTimeOffset DeletedAt);
