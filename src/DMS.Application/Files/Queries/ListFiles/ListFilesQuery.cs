using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.ListFiles;

public sealed record ListFilesQuery(
    string UserId,
    string? IpAddress) : IRequest<Result<IReadOnlyList<FileMetadataDto>>>, IAuditableRequest
{
    public string Action => "File.List";
    public string ResourceType => "File";
    public string? ResourceId => null;
}
