using DMS.Application.Services;
using DMS.Application.Settings;
using DMS.Domain.Repositories;
using DMS.Domain.Services;
using DMS.Infrastructure.Auth;
using DMS.Infrastructure.Persistence;
using DMS.Infrastructure.Persistence.Repositories;
using DMS.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtSettings>(opts => configuration.Bind("Jwt", opts));

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
