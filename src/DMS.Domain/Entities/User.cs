using DMS.Domain.Enums;

namespace DMS.Domain.Entities;

/// <summary>
/// Domain entity representing an authenticated user account.
///
/// Why sealed?
///   Domain entities model real-world invariants. Allowing subclassing risks
///   breaking invariants enforced by private setters and the Create() factory
///   (e.g., a subclass could expose a public setter). Sealed eliminates that risk.
///
/// Why private setters on all properties?
///   External code cannot reach in and mutate state directly. All state transitions
///   (create, suspend, activate) must go through the methods defined on this class,
///   which can enforce business rules. This is the "Tell, Don't Ask" principle applied
///   to domain entities.
///
/// Why no public constructor?
///   The private parameterless constructor exists solely for EF Core. EF Core uses
///   reflection to materialise entities when reading from the database — it needs a
///   no-arg constructor it can invoke without going through the factory. Making it
///   private prevents application code from ever creating an uninitialised User.
///   All application code must go through Create(), which enforces all invariants.
/// </summary>
public sealed class User
{
    /// <summary>Application-generated primary key. EF config uses ValueGeneratedNever() so EF will not try to generate this server-side.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Normalised email address (always lower-case).
    /// Stored lower-case because email addresses are case-insensitive by RFC 5321.
    /// Normalising at write time means equality comparisons and unique index lookups
    /// work correctly without case-insensitive collation at the database level.
    /// </summary>
    public string Email { get; private set; } = default!;

    /// <summary>
    /// BCrypt hash of the user's password. Never the plaintext password.
    /// The hash includes the salt (BCrypt embeds it) so no separate salt column is needed.
    /// Only IPasswordHasher.Verify() should ever read this value.
    /// </summary>
    public string PasswordHash { get; private set; } = default!;

    /// <summary>
    /// Authorization role. Stored on the entity so it can be embedded in the JWT
    /// claims at login time without a second database lookup.
    /// </summary>
    public Role Role { get; private set; }

    /// <summary>
    /// Whether the account is allowed to authenticate.
    /// Defaults to true on creation. Set to false by Suspend(); restored by Activate().
    /// The login handler checks this after verifying credentials — a suspended user
    /// gets DomainErrors.User.Suspended (403) rather than InvalidCredentials (401),
    /// giving the user a clear signal without leaking account existence to an attacker
    /// (the credential check happens first, so an attacker with wrong credentials
    /// still gets 401 before reaching the IsActive check).
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>UTC timestamp of account creation. DateTimeOffset stores timezone offset explicitly, avoiding ambiguity.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Private parameterless constructor required by EF Core for materialisation.
    /// Application code must use Create() instead.
    /// </summary>
    private User() { }

    /// <summary>
    /// Factory method: the only legitimate way for application code to create a User.
    ///
    /// Why a static factory instead of a public constructor?
    ///   1. Enforces invariants (email normalisation, IsActive=true, UTC timestamp) in one place.
    ///   2. Names the intent: "Create a new user" is more expressive than "new User(...)".
    ///   3. Application-generated GUID matches ValueGeneratedNever() in EF config — EF will
    ///      include the Id in the INSERT rather than leaving it for the database to fill.
    ///
    /// Why email.ToLowerInvariant() here?
    ///   Normalise at the boundary, not at the query site. Every downstream consumer
    ///   (repository lookup, unique-index check, JWT subject) gets the canonical form
    ///   automatically. InvariantCulture prevents locale-specific casing bugs (e.g.,
    ///   Turkish 'I' → 'ı' in ToLower() with a Turkish locale).
    ///
    /// Why Role.User as the default parameter?
    ///   The vast majority of registrations are ordinary users. Callers that need to
    ///   create admins pass Role.Admin explicitly; all others get the safe default.
    /// </summary>
    public static User Create(string email, string passwordHash, Role role = Role.User)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Deactivates the account. Subsequent login attempts return DomainErrors.User.Suspended.
    /// Called by admin-level operations (not yet wired to an endpoint in Level 1).
    /// </summary>
    public void Suspend() => IsActive = false;

    /// <summary>
    /// Re-activates a previously suspended account. Allows the user to log in again.
    /// </summary>
    public void Activate() => IsActive = true;
}
