using DMS.Application.Behaviours;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace DMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehaviour<,>));
        });

        return services;
    }
}
