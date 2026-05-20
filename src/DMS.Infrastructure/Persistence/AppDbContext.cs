using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence;

/// <summary>
/// EF Core database context: the unit of work and identity map for all domain entities.
///
/// Why primary constructor (DbContextOptions<AppDbContext> options)?
///   C# 12 primary constructors pass the options directly to the base DbContext(options)
///   constructor without the boilerplate of a separate constructor body. DbContextOptions
///   carries the connection string and provider configuration set in DependencyInjection.cs.
///
/// Why Scoped lifetime (registered via AddDbContext, which defaults to Scoped)?
///   A DbContext tracks entity state (Added, Modified, Deleted) across an entire HTTP
///   request. Scoped means one AppDbContext per request — all repositories in the same
///   request share the same change tracker and transaction scope. Singleton would cause
///   cross-request state leakage; Transient would break the unit-of-work pattern (each
///   repository would have its own change tracker that cannot see the other's entities).
///
/// Why public sealed?
///   DbContext is not designed for arbitrary subclassing chains. sealed prevents creating
///   a second context that wraps this one, which would produce confusing double-tracking.
///   public is required because the EF Core migrations tooling (dotnet ef) needs to
///   instantiate AppDbContext at design time to generate migration files.
///
/// Level 1 additions over Level 0: Users, RefreshTokens, and AuditLogs DbSets.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// DbSet computed properties use => Set<T>() rather than { get; set; } auto-properties.
    ///
    /// Why => Set<T>() instead of a conventional auto-property?
    ///   Set<T>() is EF Core's official way to retrieve a DbSet that is always backed by
    ///   the current context instance. An auto-property (public DbSet<T> Prop { get; set; })
    ///   would require manual initialisation in the constructor, which is unnecessary boilerplate
    ///   with primary constructors. The computed form also prevents the DbSet from being
    ///   accidentally null-initialised or reassigned.
    /// </summary>
    public DbSet<FileMetadata> FileMetadata => Set<FileMetadata>();

    /// <summary>Registered users. Unique index on Email enforced by UserConfiguration.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Opaque refresh tokens. Unique index on Token; non-unique index on UserId for
    /// "list all tokens for this user" queries (e.g., future "logout all devices" endpoint).
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Append-only audit trail. Indexed on UserId and OccurredAt.
    /// No DbSet method ever calls Update() or Remove() on this set — enforced structurally
    /// by IAuditLogRepository exposing only AddAsync.
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>User-created folder tree (adjacency list). Global query filter excludes soft-deleted rows.</summary>
    public DbSet<Folder> Folders => Set<Folder>();

    /// <summary>Per-file version history. One row per upload of the same filename in the same folder.</summary>
    public DbSet<FileVersion> FileVersions => Set<FileVersion>();

    /// <summary>Access control list: one row per (resource, grantee, role) triple.</summary>
    public DbSet<Share> Shares => Set<Share>();

    /// <summary>Token-based public links for unauthenticated access to files and folders.</summary>
    public DbSet<PublicLink> PublicLinks => Set<PublicLink>();

    /// <summary>
    /// Applies all IEntityTypeConfiguration<T> implementations discovered via reflection.
    ///
    /// Why ApplyConfigurationsFromAssembly instead of individual modelBuilder.ApplyConfiguration() calls?
    ///   Auto-discovery: any new IEntityTypeConfiguration<T> class added to the Infrastructure
    ///   assembly is automatically picked up without requiring a manual registration here.
    ///   This follows the Open/Closed Principle — adding a new entity does not require
    ///   modifying OnModelCreating.
    ///
    /// typeof(AppDbContext).Assembly targets the DMS.Infrastructure assembly (where the
    /// configuration classes live). Using a stable type from the same assembly avoids
    /// fragile string-based assembly names.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
