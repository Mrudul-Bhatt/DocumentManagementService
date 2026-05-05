using DMS.Application.Behaviours;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace DMS.Application;

/// <summary>
/// Registers all DMS.Application services into the DI container.
/// Called once from Program.cs during the service registration phase.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            // Scans the Application assembly and registers every IRequestHandler<,>
            // implementation automatically. New commands and queries are discovered
            // without any registration change here.
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            // Registers AuditLoggingBehaviour as an open-generic pipeline behaviour.
            // The open generic typeof(IPipelineBehavior<,>) → typeof(AuditLoggingBehaviour<,>)
            // tells MediatR to wrap every request/response pair with this behaviour.
            // MediatR closes the generic at resolve time for each concrete TRequest/TResponse.
            //
            // Behaviour execution order = registration order. If more behaviours are added
            // (e.g., a ValidationBehaviour), their order here determines the wrapping sequence:
            //   outermost → ValidationBehaviour → AuditLoggingBehaviour → handler → ...
            // Audit logging is placed after validation so failed validation requests
            // are never audited (they don't reach the audit behaviour's next() call).
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehaviour<,>));
        });

        return services;
    }
}
