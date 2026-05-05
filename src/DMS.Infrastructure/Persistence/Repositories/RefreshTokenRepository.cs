using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IRefreshTokenRepository.
/// </summary>
internal sealed class RefreshTokenRepository(AppDbContext dbContext) : IRefreshTokenRepository
{
    /// <summary>
    /// Retrieves a refresh token by its raw string value.
    ///
    /// Why FirstOrDefaultAsync and not FindAsync?
    ///   FindAsync only works with the entity's primary key (Id). The Token column is not
    ///   the PK — it is a separate uniquely-indexed column. FirstOrDefaultAsync translates
    ///   to SELECT TOP 1 ... WHERE Token = @token, which uses IX_RefreshTokens_Token efficiently.
    ///
    /// Returns null if no matching token exists. The caller (Refresh/Revoke handler) maps
    /// null → Token.Invalid → 401 without an exception crossing the layer boundary.
    /// </summary>
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token, ct);

    /// <summary>
    /// Inserts a new RefreshToken row. Called on every Login and Register (new token issued)
    /// and every Refresh (old token revoked, new token added).
    /// </summary>
    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        await dbContext.RefreshTokens.AddAsync(refreshToken, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Persists a mutation to an existing RefreshToken — specifically the RevokedAt
    /// timestamp set by RefreshToken.Revoke(). Update() marks all properties as Modified,
    /// generating an UPDATE statement for the full row. SaveChangesAsync flushes it.
    /// </summary>
    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        dbContext.RefreshTokens.Update(refreshToken);
        await dbContext.SaveChangesAsync(ct);
    }
}
