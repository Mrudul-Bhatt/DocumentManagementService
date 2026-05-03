using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Queries.DownloadFile;

/// <summary>
/// CQRS query that requests the content and metadata of a single file for streaming.
///
/// What is a Query (vs a Command)?
///   A Query reads state without changing it. It carries the parameters needed to
///   locate the resource and returns a result containing the data. Keeping reads
///   and writes in separate types (CQRS) makes the intent of each operation
///   explicit and allows them to be optimised independently (e.g., queries can
///   use read replicas; commands must use the primary database).
///
/// Why return Result<FileDownloadResult> instead of FileDownloadResult directly?
///   The file may not exist (404) or may belong to a different user (403). Wrapping
///   the return in Result makes these failure cases explicit in the handler's
///   signature — the controller is forced to check IsFailure before accessing Value.
/// </summary>
public sealed record DownloadFileQuery(
    /// <summary>Unique identifier of the file to download.</summary>
    Guid FileId,

    /// <summary>Identity of the requesting user, used to verify file ownership before streaming.</summary>
    string UserId) : IRequest<Result<FileDownloadResult>>;
