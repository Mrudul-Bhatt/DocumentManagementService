using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Folders.Commands.RenameFolder;

public sealed record RenameFolderCommand(
    Guid FolderId,
    string NewName,
    Guid UserId,
    string? IpAddress) : IRequest<Result>, IAuditableRequest
{
    public string Action                    => "Folder.Rename";
    public string ResourceType              => "Folder";
    public string? ResourceId               => FolderId.ToString();
    string IAuditableRequest.UserId         => UserId.ToString();
}
