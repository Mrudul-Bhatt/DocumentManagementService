namespace DMS.Application.Common;

/// <summary>
/// Marker interface that opts a MediatR command or query into audit logging.
///
/// How it works:
///   AuditLoggingBehaviour is a MediatR pipeline behaviour that wraps every handler.
///   On each invocation it checks whether the request implements this interface.
///   If it does — and the operation succeeded — an AuditLog record is written.
///   If it does not, the behaviour is a transparent pass-through with zero overhead.
///
/// Why an interface instead of a base record/class?
///   Commands and queries already inherit from MediatR's IRequest<T>. C# records
///   support multiple interface implementation but not multiple inheritance. An
///   interface lets any command or query opt in without changing its type hierarchy.
///   It also keeps the audit contract explicit: the presence of IAuditableRequest
///   on a type is a clear signal to reviewers that this operation is audited.
///
/// Why is ResourceId nullable?
///   Some operations don't target a specific resource — for example, File.List
///   operates on a collection rather than a single item. Nullable ResourceId
///   accommodates both collection-scoped and item-scoped operations in the same model.
///
/// Why is IpAddress nullable?
///   IP address may be unavailable when the connection comes through certain reverse
///   proxies, or when the handler is invoked from non-HTTP contexts (tests, background
///   jobs). Nullable prevents a missing IP from blocking the entire operation.
/// </summary>
public interface IAuditableRequest
{
    /// <summary>ID of the user performing the action. Written to the audit log record.</summary>
    string UserId { get; }

    /// <summary>
    /// Human-readable action name in "{Aggregate}.{Operation}" format (e.g., "File.Upload").
    /// Used as the action field in the audit log — stable across code changes so
    /// audit queries remain valid as implementations evolve.
    /// </summary>
    string Action { get; }

    /// <summary>
    /// The type of resource being acted upon (e.g., "File", "User").
    /// Enables filtering audit logs by resource type across all operations.
    /// </summary>
    string ResourceType { get; }

    /// <summary>
    /// The specific resource identifier, if applicable (e.g., a file GUID).
    /// Null for collection-scoped operations like File.List.
    /// </summary>
    string? ResourceId { get; }

    /// <summary>IP address of the caller, sourced from HttpContext.Connection.RemoteIpAddress.</summary>
    string? IpAddress { get; }
}
