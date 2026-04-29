using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Files.Commands.DeleteFile;

public sealed record DeleteFileCommand(
    Guid FileId,
    string UserId,
    string? IpAddress) : IRequest<Result>, IAuditableRequest
{
    public string Action => "File.Delete";
    public string ResourceType => "File";
    public string? ResourceId => FileId.ToString();
}
