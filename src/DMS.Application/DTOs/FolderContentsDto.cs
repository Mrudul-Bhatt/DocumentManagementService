namespace DMS.Application.DTOs;

public sealed record FolderContentsDto(
    IReadOnlyList<FolderDto> Folders,
    IReadOnlyList<FileMetadataDto> Files);
