using DMS.Application.Common;
using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DMS.Application.Behaviours;

internal sealed class AuditLoggingBehaviour<TRequest, TResponse>(
    IAuditLogRepository auditLogRepository,
    ILogger<AuditLoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (request is not IAuditableRequest auditable)
            return await next();

        var response = await next();

        // Only write the audit log entry when the operation succeeded
        var isSuccess = response is Result result ? result.IsSuccess : true;
        if (!isSuccess)
            return response;

        try
        {
            var log = AuditLog.Create(
                auditable.UserId,
                auditable.Action,
                auditable.ResourceType,
                auditable.ResourceId,
                auditable.IpAddress);

            await auditLogRepository.AddAsync(log, ct);
        }
        catch (Exception ex)
        {
            // Audit log failure must never break the primary operation
            logger.LogError(ex, "Failed to write audit log for action {Action} by user {UserId}",
                auditable.Action, auditable.UserId);
        }

        return response;
    }
}
