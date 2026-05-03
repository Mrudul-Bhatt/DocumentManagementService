using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.ListFiles;

/// <summary>
/// CQRS query that requests the metadata list for all files owned by a user.
///
/// Why IReadOnlyList<FileMetadataDto> as the value type?
///   The result is a fixed snapshot of the user's files at the time of the query.
///   IReadOnlyList communicates that the caller should not (and cannot) mutate the
///   collection — it is read-once data to be serialised and returned. Using the
///   interface type (rather than List<T>) keeps the handler free to return any
///   concrete collection type without breaking the contract.
///
/// Ownership scoping:
///   UserId is the only parameter because the query scope is always "all files for
///   this user". The handler passes it directly to the repository, which filters
///   at the database level. Callers never see files belonging to other users.
/// </summary>
public sealed record ListFilesQuery(
    /// <summary>Identity of the requesting user. The result is filtered to only their files.</summary>
    string UserId) : IRequest<Result<IReadOnlyList<FileMetadataDto>>>;
