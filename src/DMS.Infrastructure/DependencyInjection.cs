using DMS.Domain.Repositories;
using DMS.Domain.Services;
using DMS.Infrastructure.Persistence;
using DMS.Infrastructure.Persistence.Repositories;
using DMS.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DMS.Infrastructure;

/// <summary>
/// Registers all DMS.Infrastructure services into the DI container.
///
/// Why does this method receive IConfiguration when DMS.Application.DependencyInjection does not?
///   Infrastructure is the only layer that is allowed to read raw IConfiguration directly.
///   It needs connection strings (database), file paths (storage), and in later levels,
///   secrets (JWT signing key, email provider credentials). Passing IConfiguration here
///   keeps it out of Application and Domain, which must remain infrastructure-agnostic.
///
/// What counts as an "Infrastructure layer" service?
///   - Database access: DbContext, EF Core configuration, repositories
///   - External storage: file system, cloud blob storage
///   - External services: email providers, token generators, password hashers (Level 1+)
///   Nothing here should contain business logic — that belongs in DMS.Application and DMS.Domain.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds all Infrastructure layer services to the DI container.
    /// Called once from Program.cs during the service registration phase.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // AddDbContext registers AppDbContext as Scoped (default lifetime for DbContext).
        // Scoped means one instance per HTTP request — all repository calls within
        // one request share the same DbContext, its identity map, and change tracker.
        // UseSqlServer wires EF Core to SQL Server using the connection string from
        // appsettings.json. The connection string contains server, database, and
        // credentials — it must never be hardcoded here; configuration is the source.
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // AddScoped registers one instance per HTTP request, matching AppDbContext's
        // lifetime. This is critical: if the repository outlived the DbContext (e.g.,
        // Singleton repository with Scoped DbContext), EF Core would throw a
        // "Cannot access a disposed DbContext" error on the second request.
        //
        // The interface (IFileMetadataRepository) is registered against its concrete
        // implementation (FileMetadataRepository). Handlers depend only on the interface —
        // swapping the implementation requires changing only this one line.
        services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();

        // LocalFileStorageService is also Scoped for lifetime consistency.
        // It holds no per-request state of its own, so Singleton would work too —
        // but Scoped is the safer default: it avoids potential issues if a future
        // implementation captures request-scoped dependencies (e.g., a current user
        // context for audit logging).
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
