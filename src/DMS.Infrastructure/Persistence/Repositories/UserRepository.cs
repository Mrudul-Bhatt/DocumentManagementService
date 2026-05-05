using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IUserRepository.
///
/// Why internal sealed?
///   This is an infrastructure detail. Application code depends on IUserRepository
///   (defined in the Domain layer), never on this concrete class. sealed prevents
///   unintended subclassing within Infrastructure.
///
/// Why primary constructor injection of AppDbContext?
///   Primary constructors (C# 12) eliminate the boilerplate of a constructor body and
///   a private readonly field declaration. AppDbContext is Scoped — the same instance is
///   shared by all repositories in the same HTTP request, ensuring they participate in
///   the same unit of work (change tracker and SaveChangesAsync call).
/// </summary>
internal sealed class UserRepository(AppDbContext dbContext) : IUserRepository
{
    /// <summary>
    /// Looks up a user by their primary key using EF Core's FindAsync.
    ///
    /// Why FindAsync instead of FirstOrDefaultAsync(u => u.Id == id)?
    ///   FindAsync checks the change tracker's identity map first — if the entity was
    ///   already loaded earlier in the same request (e.g., by another repository method),
    ///   it is returned from memory without a database round-trip. FirstOrDefaultAsync
    ///   always issues a SQL query. For PK lookups, FindAsync is the preferred method.
    ///
    /// The array syntax [id] satisfies FindAsync's params object[] keys parameter.
    /// </summary>
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Users.FindAsync([id], ct);

    /// <summary>
    /// Looks up a user by their normalised email address.
    ///
    /// Why .ToLowerInvariant() here as well as in User.Create()?
    ///   Defence in depth: even if a caller passes a mixed-case email (e.g., "User@Example.COM"),
    ///   the WHERE clause matches against the stored lower-case value in the database.
    ///   This prevents a case-mismatch "not found" failure during login if the client
    ///   sends the email with a different case than the one used at registration.
    ///
    /// Why FirstOrDefaultAsync (not FindAsync)?
    ///   FindAsync only works with the primary key. Email is not the PK — it is a
    ///   uniquely indexed column. FirstOrDefaultAsync translates to
    ///   SELECT TOP 1 ... WHERE Email = @email, which uses IX_Users_Email efficiently.
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    /// <summary>
    /// Inserts a new User row and immediately flushes to the database.
    ///
    /// Why SaveChangesAsync after every Add/Update?
    ///   This repository follows an "immediate flush" pattern: each mutation is committed
    ///   independently rather than accumulating changes across multiple method calls.
    ///   This avoids subtle bugs where a caller forgets to call SaveChangesAsync at the
    ///   end of a unit of work. The trade-off is that multiple mutations in one request
    ///   each issue a separate round-trip rather than batching into one commit.
    /// </summary>
    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await dbContext.Users.AddAsync(user, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Persists mutations to an existing User entity (e.g., after Suspend() or Activate()).
    ///
    /// Why dbContext.Users.Update(user)?
    ///   Update() marks all properties of the entity as Modified in the change tracker.
    ///   This generates an UPDATE statement that sets every column, not just the changed ones.
    ///   Since User entities are small and updates are infrequent, the overhead of
    ///   updating all columns is negligible compared to the complexity of tracking
    ///   individual property changes.
    ///
    /// Why is this needed? Can't EF Core detect changes automatically?
    ///   Automatic change detection works when the entity was loaded from the same DbContext
    ///   instance (it is in the identity map). If the entity was detached (e.g., loaded in
    ///   a different scope and passed in), explicit Update() is required to re-attach it.
    /// </summary>
    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(ct);
    }
}
