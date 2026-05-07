using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Folders.Commands.DeleteFolder;

public sealed record DeleteFolderCommand(
    Guid FolderId,
    Guid UserId,
    string? IpAddress) : IRequest<Result>, IAuditableRequest
{
    public string Action                    => "Folder.Delete";
    public string ResourceType              => "Folder";
    public string? ResourceId               => FolderId.ToString();
    string IAuditableRequest.UserId         => UserId.ToString();
}
