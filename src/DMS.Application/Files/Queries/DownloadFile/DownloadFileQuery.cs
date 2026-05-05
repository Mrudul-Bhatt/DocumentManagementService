using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.DownloadFile;

/// <summary>
/// CQRS query that requests the content and metadata of a single file for streaming.
///
/// Why a Query (not a Command) even though it implements IAuditableRequest?
///   In CQRS, Queries are read operations. This query does not mutate state — it
///   reads bytes from storage. IAuditableRequest is an orthogonal concern (auditing)
///   that can apply to both commands and queries. Downloading a file is a
///   security-relevant read event worth recording in the audit log.
///
/// Level 1 additions over Level 0:
///   - IpAddress parameter for audit logging.
///   - IAuditableRequest: AuditLoggingBehaviour writes an AuditLog record with
///     Action="File.Download" and ResourceId=FileId after the handler succeeds.
/// </summary>
public sealed record DownloadFileQuery(
    /// <summary>Unique identifier of the file to download.</summary>
    Guid FileId,
    /// <summary>Identity of the authenticated user. The handler verifies ownership before streaming.</summary>
    string UserId,
    /// <summary>Caller's IP address for audit logging.</summary>
    string? IpAddress) : IRequest<Result<FileDownloadResult>>, IAuditableRequest
{
    public string Action       => "File.Download";
    public string ResourceType => "File";
    public string? ResourceId  => FileId.ToString();
}
