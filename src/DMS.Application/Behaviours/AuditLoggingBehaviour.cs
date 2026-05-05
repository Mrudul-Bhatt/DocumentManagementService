using DMS.Application.Common;
using DMS.Domain.Entities;
using DMS.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DMS.Application.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that writes an AuditLog record for every command or
/// query that implements IAuditableRequest, but only on successful operations.
///
/// What is a pipeline behaviour?
///   IPipelineBehavior&lt;TRequest, TResponse&gt; is MediatR's middleware pattern.
///   Registered behaviours wrap every handler invocation in a chain — similar to
///   ASP.NET Core middleware but operating at the application layer, not the HTTP layer.
///   Each behaviour calls "next()" to invoke the rest of the pipeline (ultimately the
///   handler itself) and can inspect the request before and/or the response after.
///
/// Why here instead of in each handler?
///   Cross-cutting concerns that apply to many handlers should not be duplicated across
///   all of them. A pipeline behaviour adds audit logging to any command or query in
///   one place. Adding a new auditable operation requires only implementing IAuditableRequest
///   on the command/query — the behaviour picks it up automatically with no code changes here.
///
/// Why internal sealed?
///   This is an Application layer implementation detail. Nothing outside the assembly
///   should reference it directly — it is registered via the open generic
///   IPipelineBehavior&lt;,&gt; in DependencyInjection.cs and invoked by MediatR.
///
/// Opt-in design:
///   Only requests implementing IAuditableRequest are audited. Requests that do not
///   implement it pass through this behaviour as a transparent no-op — the early return
///   on the interface check incurs negligible overhead.
/// </summary>
internal sealed class AuditLoggingBehaviour<TRequest, TResponse>(
    IAuditLogRepository auditLogRepository,
    ILogger<AuditLoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Invoked by MediatR for every request before the handler runs.
    ///
    /// Flow:
    ///   1. Check if the request opts into auditing — if not, pass through immediately.
    ///   2. Execute the handler via next().
    ///   3. If the operation failed, return without writing an audit log.
    ///   4. Write the audit log entry. If that write fails, log the error and return
    ///      the original response — audit failure must never break the primary operation.
    ///
    /// Why audit after the handler (post-processing) rather than before?
    ///   An audit log should record what actually happened, not what was attempted.
    ///   Auditing before the handler would log operations that subsequently fail,
    ///   producing misleading records (e.g., "File uploaded" when the upload failed).
    ///
    /// Why only audit on success?
    ///   Failed operations are captured in structured logs via LogWarning/LogError in
    ///   the individual handlers. The audit log is a business-level record of committed
    ///   actions — it answers "what did user X do?" not "what did user X try to do?".
    ///
    /// Why the try/catch around the audit write?
    ///   The audit log is a secondary concern. If writing to the audit table fails
    ///   (transient DB error, network blip), the primary operation has already succeeded
    ///   and its result has been returned. Letting the audit write throw would roll back
    ///   or hide a successfully completed operation from the caller — unacceptable.
    ///   The error is logged so the gap can be investigated without silently discarding it.
    /// </summary>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Fast path: non-auditable requests pass through with no overhead.
        if (request is not IAuditableRequest auditable)
            return await next();

        // Execute the primary handler first — audit is always post-operation.
        var response = await next();

        // Inspect the response to determine success.
        // If TResponse is Result (or Result<T>), use IsSuccess. If it is not a Result
        // type (rare for this project but possible in future), default to true.
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
            // Audit log failure must never surface to the caller or break the operation.
            // Log the exception so it can be investigated, then return the successful response.
            logger.LogError(ex, "Failed to write audit log for action {Action} by user {UserId}",
                auditable.Action, auditable.UserId);
        }

        return response;
    }
}
