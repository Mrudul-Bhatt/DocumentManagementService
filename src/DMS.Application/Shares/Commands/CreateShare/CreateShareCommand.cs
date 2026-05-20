using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Enums;
using MediatR;

namespace DMS.Application.Shares.Commands.CreateShare;

public sealed record CreateShareCommand(
    Guid ResourceId,
    ShareResourceType ResourceType,
    string GrantedToEmail,
    ShareRole Role,
    Guid GrantedByUserId,
    string? IpAddress) : IRequest<Result<ShareDto>>, IAuditableRequest
{
    public string Action => "Share.Create";

    // Explicit implementations resolve the type mismatch between the Guid/enum record
    // properties and the string members required by IAuditableRequest.
    string IAuditableRequest.UserId        => GrantedByUserId.ToString();
    string IAuditableRequest.ResourceType  => ResourceType.ToString();
    string? IAuditableRequest.ResourceId   => ResourceId.ToString();
}
