using DMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DMS.Infrastructure.Persistence;

/// <summary>
/// EF Core database context — the single entry point for all database access in DMS.
///
/// What is a DbContext?
///   DbContext is EF Core's unit of work and identity map. It tracks entity instances
///   loaded during a request, accumulates changes (Add, Remove, property mutations),
///   and flushes them to the database in a single transaction when SaveChangesAsync()
///   is called. One DbContext instance represents one logical unit of work.
///
/// Why sealed?
///   AppDbContext is a concrete infrastructure type with no intended subclasses.
///   sealed prevents accidental inheritance that could bypass EF Core's internal
///   state management or intercept change tracking unexpectedly.
///
/// Lifetime — Scoped:
///   AppDbContext is registered as Scoped in DependencyInjection.cs, meaning one
///   instance is created per HTTP request and disposed when the request ends.
///   This aligns the unit-of-work boundary with the request boundary: all operations
///   within one request share the same context and its identity map, but no state
///   leaks between requests.
///
/// Why no business logic here?
///   AppDbContext is pure infrastructure — it configures mappings and provides
///   DbSet access. All business rules live in the Domain layer. Putting query logic
///   or domain decisions here would make them invisible to the Application layer
///   and untestable without a database.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Provides access to the FileMetadata table for LINQ queries and change tracking.
    ///
    /// Why a computed property (=> Set<FileMetadata>()) instead of a backing field?
    ///   Set<T>() returns EF Core's DbSet for the type, which is always in sync with
    ///   the context's internal state. A { get; set; } auto-property would require
    ///   EF Core to assign it via reflection at context construction time. The
    ///   expression-bodied form is simpler and avoids the nullable warning that comes
    ///   with an uninitialized DbSet property.
    /// </summary>
    public DbSet<FileMetadata> FileMetadata => Set<FileMetadata>();

    /// <summary>
    /// Applies all Fluent API entity configurations defined in this assembly.
    ///
    /// Why Fluent API over Data Annotations ([Key], [MaxLength], etc.)?
    ///   Data annotations couple infrastructure concerns (column names, max lengths,
    ///   indexes) to the Domain entity class. The Domain layer should not reference
    ///   EF Core attributes — that would create a dependency from Domain to Infrastructure,
    ///   inverting the intended direction. Fluent API keeps all mapping logic in
    ///   separate IEntityTypeConfiguration<T> classes inside Infrastructure.
    ///
    /// Why ApplyConfigurationsFromAssembly instead of calling each configuration explicitly?
    ///   The assembly scan discovers every IEntityTypeConfiguration<T> implementation
    ///   automatically. Adding a new entity requires only a new configuration class —
    ///   no change to OnModelCreating. Explicit calls would require this method to be
    ///   updated every time a new entity is introduced, risking a missed registration.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
