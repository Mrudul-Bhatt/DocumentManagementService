using DMS.Application.Services;
using DMS.Application.Settings;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using DMS.Infrastructure.Auth;
using DMS.Infrastructure.BackgroundServices;
using DMS.Infrastructure.Persistence;
using DMS.Infrastructure.Persistence.Repositories;
using DMS.Infrastructure.Services;
using DMS.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DMS.Infrastructure;

/// <summary>
/// Extension method that registers all DMS.Infrastructure services into the DI container.
/// Called from DMS.Api's Program.cs: builder.Services.AddInfrastructure(builder.Configuration).
///
/// Why does this method accept IConfiguration?
///   Infrastructure is the only layer permitted to read raw configuration (connection strings,
///   file paths). Application layer classes use the Options pattern instead (IOptions<T>).
///   IConfiguration is passed in from the API layer so that Infrastructure can read
///   appsettings.json values without creating a direct dependency from Infrastructure on the
///   ASP.NET Core host.
///
/// Why a static extension method on IServiceCollection?
///   Keeps Program.cs clean ("add infrastructure" as a single line) and encapsulates all
///   Infrastructure registrations here — the API layer has no knowledge of which concrete
///   classes implement which interfaces.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind the "Jwt" section of appsettings.json to JwtSettings (defined in Application).
        // Configure<T> registers IOptions<JwtSettings> so that JwtTokenService and the JWT
        // Bearer middleware can resolve the settings via constructor injection.
        // Using Bind() (rather than GetSection()) allows binding to a pre-existing options
        // instance, which is required by the lambda overload.
        services.Configure<JwtSettings>(opts => configuration.Bind("Jwt", opts));

        // AddDbContext registers AppDbContext as Scoped (one instance per HTTP request).
        // UseSqlServer wires the SQL Server provider with the connection string from
        // "ConnectionStrings:DefaultConnection" in appsettings.json / environment variables.
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // ── Repositories — Scoped ────────────────────────────────────────────────────────
        // All repositories must be Scoped to match AppDbContext's lifetime.
        // A Singleton repository holding a Scoped DbContext would cause an ObjectDisposedException
        // on the second request (the first request disposes the DbContext; the Singleton still
        // holds the dead reference).
        services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();
        services.AddScoped<IFolderRepository, FolderRepository>();
        services.AddScoped<IFileVersionRepository, FileVersionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // ── Storage — Scoped ─────────────────────────────────────────────────────────────
        // LocalFileStorageService injects IConfiguration, which is Singleton. Scoped is fine
        // here; the service holds no per-request state. Registering as Scoped keeps it aligned
        // with the other services without over-sharing across requests.
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        // ── Auth services — Singleton ─────────────────────────────────────────────────────
        // PasswordHasher and JwtTokenService are stateless pure-function services.
        // They hold no mutable state and are safe to share across all requests and threads.
        // Singleton avoids re-allocating these objects on every request, which is a minor
        // but correct optimisation. Both only depend on IOptions<T>, which is also Singleton.
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        services.AddScoped<IEmailService, EmailService>();

        // ── Application services — Scoped ────────────────────────────────────────────────
        // TrashPurgeService injects AppDbContext (Scoped), so it must also be Scoped.
        // The background service creates a new scope per invocation via IServiceScopeFactory.
        services.AddScoped<ITrashPurgeService, TrashPurgeService>();

        // ── Background services ───────────────────────────────────────────────────────────
        // AddHostedService registers as Singleton (host lifecycle). The service uses
        // IServiceScopeFactory to resolve Scoped services per invocation.
        services.AddHostedService<TrashPurgeBackgroundService>();

        return services;
    }
}
