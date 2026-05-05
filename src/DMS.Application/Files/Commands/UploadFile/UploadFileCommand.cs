using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Commands.UploadFile;

/// <summary>
/// CQRS command that instructs the application to persist a new file on behalf of a user.
///
/// Level 1 additions over Level 0:
///   - IpAddress parameter: sourced from HttpContext.Connection.RemoteIpAddress in the
///     controller, passed through to satisfy IAuditableRequest for the AuditLoggingBehaviour.
///   - IAuditableRequest: opts this command into post-success audit logging.
///     AuditLoggingBehaviour writes an AuditLog record with Action="File.Upload" after
///     the handler succeeds.
///
/// Why ResourceId => null for upload?
///   The file's ID is not known until after the handler persists it and generates
///   the GUID. The audit log is written after the handler returns, but the record's
///   ResourceId is bound from the command at construction time — before the ID exists.
///   Future improvement: return the new file ID from the handler as part of the Result
///   and write the audit log with the resolved ID inside the handler itself.
/// </summary>
public sealed record UploadFileCommand(
    /// <summary>Identity of the authenticated user performing the upload.</summary>
    string UserId,
    /// <summary>Original filename as provided by the HTTP client.</summary>
    string Filename,
    /// <summary>MIME type reported by the HTTP client (e.g., "application/pdf").</summary>
    string MimeType,
    /// <summary>File size in bytes. Validated against the 25 MB cap before the stream is read.</summary>
    long FileSize,
    /// <summary>Raw readable stream of file bytes from the HTTP request body.</summary>
    Stream Content,
    /// <summary>Caller's IP address for audit logging. Nullable — unavailable behind some proxies.</summary>
    string? IpAddress) : IRequest<Result<FileMetadataDto>>, IAuditableRequest
{
    public string Action       => "File.Upload";
    public string ResourceType => "File";
    public string? ResourceId  => null;   // ID not yet known at command construction time
}
