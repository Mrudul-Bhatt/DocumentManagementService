namespace DMS.Domain.Entities;

/// <summary>
/// Domain entity representing an immutable audit trail entry.
///
/// Why append-only? (No Update, no Delete, no domain mutating methods)
///   An audit log exists to provide a tamper-evident record of what happened.
///   If records could be updated or deleted, the log would lose its trustworthiness
///   as evidence. IAuditLogRepository exposes only AddAsync to enforce this at the
///   repository boundary — there is no pathway for application code to modify an
///   existing audit entry.
///
/// Who writes these?
///   AuditLoggingBehaviour (a MediatR pipeline behaviour) creates an AuditLog after
///   every successful handler execution for requests that implement IAuditableRequest.
///   No handler writes audit logs directly; the cross-cutting behaviour handles it
///   transparently for all opted-in commands and queries.
///
/// Why store UserId as string here but Guid on User?
///   The audit log captures a user identifier for human-readable forensic records.
///   Using string avoids coupling the log to a specific ID type — if the system ever
///   adds system-generated events with a non-Guid actor, the log schema does not need
///   to change. The convention used throughout Level 1 is the string representation
///   of the Guid (via User.Id.ToString()).
/// </summary>
public sealed class AuditLog
{
    /// <summary>Application-generated primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// String representation of the authenticated user's Guid.
    /// Sourced from the JWT subject claim (ClaimTypes.NameIdentifier) in the controller,
    /// then forwarded through the command/query to AuditLoggingBehaviour.
    /// </summary>
    public string UserId { get; private set; } = default!;

    /// <summary>
    /// Human-readable action identifier. Convention: "ResourceType.Verb" — e.g.,
    /// "File.Upload", "File.Download", "File.Delete", "File.List".
    /// Sourced from IAuditableRequest.Action on the triggering command or query.
    /// </summary>
    public string Action { get; private set; } = default!;

    /// <summary>
    /// The category of resource the action operated on (e.g., "File").
    /// Sourced from IAuditableRequest.ResourceType. Enables filtering audit logs
    /// by resource category without string-parsing the Action field.
    /// </summary>
    public string ResourceType { get; private set; } = default!;

    /// <summary>
    /// Optional: the specific resource's identifier (e.g., the file's GUID string).
    /// Null for collection-scoped operations (e.g., File.List) and for commands where
    /// the ID is not yet known at request construction time (e.g., File.Upload — the
    /// GUID is generated inside the handler, after the audit record's fields are bound).
    /// </summary>
    public string? ResourceId { get; private set; }

    /// <summary>
    /// Optional: the caller's IP address, sourced from HttpContext.Connection.RemoteIpAddress.
    /// Nullable because it may be unavailable behind certain reverse proxies or load
    /// balancers that do not forward the originating IP.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// UTC timestamp of when the action occurred (i.e., when AuditLoggingBehaviour ran).
    /// Named OccurredAt rather than CreatedAt to reflect the domain meaning: this is
    /// when the auditable event happened, not just when the row was inserted.
    /// </summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>
    /// Private parameterless constructor required by EF Core for materialisation.
    /// Application code must use Create() instead.
    /// </summary>
    private AuditLog() { }

    /// <summary>
    /// Factory method: the only way to create an AuditLog entry.
    ///
    /// ResourceId and IpAddress are optional parameters (default null) because:
    ///   - Not every operation targets a single known resource (collection ops, upload).
    ///   - IP is not always available (proxied environments, local dev without forwarding).
    ///
    /// OccurredAt is captured inside Create() at the moment of construction, not passed
    /// in by the caller. This prevents any code from backdating or forward-dating entries.
    /// </summary>
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
