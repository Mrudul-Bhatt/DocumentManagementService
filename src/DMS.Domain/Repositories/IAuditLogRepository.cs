using DMS.Domain.Entities;

namespace DMS.Domain.Repositories;

/// <summary>
/// Repository abstraction for AuditLog persistence. Intentionally exposes only AddAsync.
///
/// Why only one method?
///   AuditLog is an append-only aggregate — entries are written once and never modified
///   or deleted (see AuditLog entity comments for the rationale). This interface enforces
///   that constraint structurally: there is no UpdateAsync or DeleteAsync to call, making
///   it impossible for application code to accidentally mutate or purge audit records.
///
/// Why no GetBy* queries?
///   Level 1 does not include an audit log viewing endpoint. When that feature is added
///   (e.g., an admin endpoint to list events for a user), GetByUserIdAsync or a paged
///   query method will be added to this interface at that time. Defining query methods
///   before they are needed would be premature and would require unnecessary implementations.
///
/// Why is this in the Domain layer?
///   Same reason as all other repository interfaces: Dependency Inversion. AuditLoggingBehaviour
///   in the Application layer depends on this interface, not on EF Core. The Infrastructure
///   implementation can be swapped (e.g., write to an external SIEM system) without changing
///   the Application or Domain layers.
/// </summary>
public interface IAuditLogRepository
{
    /// <summary>
    /// Persists a new AuditLog entry. This is the sole write operation on the audit log.
    /// Called by AuditLoggingBehaviour after every successful IAuditableRequest handler.
    ///
    /// Why is failure here intentionally swallowed by the caller (AuditLoggingBehaviour)?
    ///   Audit logging is a cross-cutting concern, not the primary operation. If the audit
    ///   write fails (e.g., transient database error), the caller's try/catch ensures the
    ///   primary response (e.g., the uploaded file's metadata) still reaches the client.
    ///   The failure is logged as a warning so it is visible in logs without surfacing as
    ///   a 500 to the user.
    /// </summary>
    Task AddAsync(AuditLog log, CancellationToken ct = default);
}
