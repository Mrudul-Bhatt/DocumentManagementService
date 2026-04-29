namespace DMS.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; } = default!;
    public string Action { get; private set; } = default!;
    public string ResourceType { get; private set; } = default!;
    public string? ResourceId { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        string userId,
        string action,
        string resourceType,
        string? resourceId = null,
        string? ipAddress = null)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            IpAddress = ipAddress,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }
}
