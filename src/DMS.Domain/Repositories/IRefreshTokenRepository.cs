using DMS.Domain.Entities;

namespace DMS.Domain.Repositories;

/// <summary>
/// Repository abstraction for RefreshToken aggregate persistence.
///
/// Why only three methods (no GetByIdAsync, no DeleteAsync)?
///   - GetByTokenAsync is the only lookup the application ever performs — the Refresh
///     and Revoke handlers receive the raw token string from the client, not its GUID.
///     There is no use case for looking up a token by its primary key from application code.
///   - DeleteAsync is intentionally absent. Expired or revoked tokens are retained in
///     the database for forensic purposes (see RefreshToken.Revoke() comments). Cleanup
///     of old tokens is a background maintenance task, not an application-layer concern.
///
/// Why is this interface in the Domain layer?
///   Dependency Inversion: the Application layer's handlers depend on this interface,
///   not on the EF Core implementation. The Infrastructure layer implements the interface,
///   keeping the dependency arrow pointing inward.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Retrieves the RefreshToken entity whose Token string matches the provided value.
    /// Returns null if no matching token exists (not found, already deleted, or never issued).
    ///
    /// Why look up by the token string and not by UserId?
    ///   The Refresh and Revoke endpoints receive only the raw token string from the client.
    ///   The entity's UserId is then read from the retrieved row — this prevents a client
    ///   from claiming to own a token by passing a UserId parameter alongside the token string.
    ///   Lookup by the opaque token value is the only safe approach.
    ///
    /// The token column has a unique index in the EF configuration, so this lookup is O(log n).
    /// </summary>
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Persists a new RefreshToken entity. Called during Register and Login (token issuance)
    /// and during Refresh (after the old token is revoked, the new one is added here).
    /// </summary>
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Persists changes to an existing RefreshToken entity. Called after RefreshToken.Revoke()
    /// sets RevokedAt — UpdateAsync flushes the mutated entity to the database.
    ///
    /// Why UpdateAsync instead of a dedicated RevokeAsync(tokenId)?
    ///   The repository follows the pattern of accepting the full entity and persisting its
    ///   current state. This keeps the repository interface thin and avoids proliferating
    ///   specialised methods for every possible state transition.
    /// </summary>
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default);
}
