using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.DownloadFile;

public sealed record DownloadFileQuery(
    Guid FileId,
    string UserId,
    string? IpAddress) : IRequest<Result<FileDownloadResult>>, IAuditableRequest
{
    public string Action => "File.Download";
    public string ResourceType => "File";
    public string? ResourceId => FileId.ToString();
}
