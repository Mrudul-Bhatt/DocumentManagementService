using DMS.Domain.Entities;

namespace DMS.Domain.Repositories;

/// <summary>
/// Repository abstraction for User aggregate persistence.
///
/// Why is this interface defined in the Domain layer?
///   Dependency Inversion Principle: the Domain should not depend on any infrastructure
///   (EF Core, SQL Server, etc.). By defining the contract here and implementing it in
///   DMS.Infrastructure, the dependency arrow points inward — Infrastructure depends on
///   Domain, never the reverse. This also makes unit testing handlers trivial: swap in
///   an in-memory fake that implements this interface.
///
/// Why no DeleteAsync?
///   User accounts are not deleted in Level 1 — they are suspended via User.Suspend().
///   Soft deletion preserves referential integrity with AuditLog (audit records reference
///   UserId; hard-deleting the user would orphan them) and the audit trail itself.
///   If hard deletion is ever needed (GDPR right-to-erasure), it would be a deliberate
///   addition with its own compliance review, not an implicit convenience method.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a user by their primary key. Returns null if no user with that ID exists.
    ///
    /// Why return null instead of throwing?
    ///   Null is the idiomatic .NET signal for "not found" at the repository boundary.
    ///   The handler maps null → DomainErrors.User.NotFound → Result.Failure, keeping
    ///   the exception-free Result pattern intact through the full call chain.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a user by their email address. Returns null if not found.
    ///
    /// Why a separate method instead of a generic Find(predicate)?
    ///   Email is the natural login key — this query runs on every login attempt.
    ///   A named method documents intent and maps directly to an indexed column
    ///   (IX_Users_Email in the EF config), guaranteeing an efficient lookup.
    ///   The email parameter should be pre-normalised (lower-case) by the caller
    ///   to match the stored canonical form.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Persists a new User entity. The entity's Id is application-generated (Guid.NewGuid()
    /// in User.Create()), so no identity value is returned — the caller already has the GUID.
    /// </summary>
    Task AddAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Persists changes to an existing User entity (e.g., after Suspend() or Activate()).
    ///
    /// Why UpdateAsync when there are no properties the application directly sets?
    ///   Domain methods like Suspend() and Activate() mutate private fields on the entity.
    ///   EF Core tracks these changes via the change tracker, but the repository still needs
    ///   to call SaveChangesAsync() to flush them to the database. UpdateAsync encapsulates
    ///   that flush without exposing EF Core's SaveChangesAsync directly to the Application layer.
    /// </summary>
    Task UpdateAsync(User user, CancellationToken ct = default);
}
