using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Files.Commands.DeleteFile;

/// <summary>
/// CQRS command that permanently deletes a file owned by the authenticated user.
///
/// Level 1 additions over Level 0:
///   - IpAddress parameter for audit logging.
///   - IAuditableRequest: AuditLoggingBehaviour writes an AuditLog record with
///     Action="File.Delete" and ResourceId=FileId after the handler succeeds.
///
/// Why IRequest&lt;Result&gt; (non-generic)?
///   Delete is a void operation — on success there is no value to return.
/// </summary>
public sealed record DeleteFileCommand(
    /// <summary>Unique identifier of the file to delete.</summary>
    Guid FileId,
    /// <summary>Identity of the authenticated user. The handler verifies ownership before deleting.</summary>
    string UserId,
    /// <summary>Caller's IP address for audit logging.</summary>
    string? IpAddress) : IRequest<Result>, IAuditableRequest
{
    public string Action       => "File.Delete";
    public string ResourceType => "File";
    public string? ResourceId  => FileId.ToString();
}
