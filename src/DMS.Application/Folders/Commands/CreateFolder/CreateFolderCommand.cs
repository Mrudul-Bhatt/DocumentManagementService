using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Folders.Commands.CreateFolder;

public sealed record CreateFolderCommand(
    string Name,
    Guid? ParentFolderId,
    Guid UserId,
    string? IpAddress) : IRequest<Result<FolderDto>>, IAuditableRequest
{
    public string Action                    => "Folder.Create";
    public string ResourceType              => "Folder";
    public string? ResourceId               => null;
    string IAuditableRequest.UserId         => UserId.ToString();
}
