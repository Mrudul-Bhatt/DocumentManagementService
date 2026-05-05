using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.ListFiles;

/// <summary>
/// CQRS query that requests the metadata list for all files owned by a user.
///
/// Level 1 additions over Level 0:
///   - IpAddress parameter for audit logging.
///   - IAuditableRequest: AuditLoggingBehaviour writes an AuditLog record with
///     Action="File.List" after the handler succeeds.
///
/// Why ResourceId => null?
///   List operates on the user's entire file collection, not a single resource.
///   There is no meaningful single ResourceId — null is the correct representation
///   for a collection-scoped operation.
/// </summary>
public sealed record ListFilesQuery(
    /// <summary>Identity of the authenticated user. Results are scoped exclusively to this user's files.</summary>
    string UserId,
    /// <summary>Caller's IP address for audit logging.</summary>
    string? IpAddress) : IRequest<Result<IReadOnlyList<FileMetadataDto>>>, IAuditableRequest
{
    public string Action       => "File.List";
    public string ResourceType => "File";
    public string? ResourceId  => null;
}
