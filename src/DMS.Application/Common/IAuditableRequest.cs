namespace DMS.Application.Common;

public interface IAuditableRequest
{
    string UserId { get; }
    string Action { get; }
    string ResourceType { get; }
    string? ResourceId { get; }
    string? IpAddress { get; }
}
