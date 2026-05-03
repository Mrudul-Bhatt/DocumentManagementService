using Microsoft.Extensions.DependencyInjection;

namespace DMS.Application;

/// <summary>
/// Registers all DMS.Application services into the DI container.
///
/// Why a static extension method on IServiceCollection?
///   This is the standard .NET composition pattern: each layer exposes one
///   AddXxx() extension method so Program.cs stays flat and layer internals
///   stay encapsulated. Program.cs calls builder.Services.AddApplication()
///   without knowing which handlers, behaviours, or services live inside.
///
/// What counts as an "Application layer" service?
///   - MediatR and all IRequestHandler implementations (commands and queries)
///   - IPipelineBehaviour implementations (validation, audit logging — added in later levels)
///   - Application-level abstractions that have no infrastructure dependency
///   Nothing in this method should reference a database, filesystem, or external API —
///   those registrations belong in DMS.Infrastructure.DependencyInjection.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds all Application layer services to the DI container.
    /// Called once from Program.cs during the service registration phase.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // RegisterServicesFromAssembly scans the Application assembly at startup and
        // registers every class that implements IRequestHandler<TRequest, TResponse>
        // as a scoped service. This means adding a new command or query handler
        // requires no change here — the handler is discovered automatically.
        //
        // typeof(DependencyInjection).Assembly is a stable anchor for the scan:
        // it refers to the DMS.Application assembly regardless of its output path,
        // and avoids hardcoding an assembly name string that could silently break
        // after a project rename.
        //
        // Level 1 addition: pipeline behaviours (IPipelineBehaviour<,>) are also
        // registered here — e.g., cfg.AddBehavior<IPipelineBehavior<,>, AuditLoggingBehaviour>()
        // They wrap every handler invocation in the order they are registered,
        // enabling cross-cutting concerns (audit logging, validation) without
        // modifying any individual handler.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
