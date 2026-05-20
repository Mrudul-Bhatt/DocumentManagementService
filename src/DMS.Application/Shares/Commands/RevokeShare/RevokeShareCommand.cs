using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Shares.Commands.RevokeShare;

public sealed record RevokeShareCommand(
    Guid ShareId,
    Guid RequestingUserId,
    string? IpAddress) : IRequest<Result>, IAuditableRequest
{
    public string Action => "Share.Revoke";

    string IAuditableRequest.UserId       => RequestingUserId.ToString();
    string IAuditableRequest.ResourceType => "Share";
    string? IAuditableRequest.ResourceId  => ShareId.ToString();
}
