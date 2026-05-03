using DMS.Application.Common;
using MediatR;

namespace DMS.Application.Files.Commands.DeleteFile;

/// <summary>
/// CQRS command that instructs the application to permanently delete a file.
///
/// Why IRequest<Result> (non-generic) instead of IRequest<Result<T>>?
///   Delete is a void operation — on success there is no value to return.
///   The non-generic Result carries only IsSuccess / Error, which is all the
///   controller needs to decide between 204 No Content and an error response.
///
/// Ownership enforcement:
///   Both FileId and UserId are required so the handler can verify the requesting
///   user owns the file before deleting it. The handler — not the controller —
///   is responsible for this check. A user who supplies another user's FileId
///   receives a 403 Forbidden, not a 404, to avoid leaking existence information.
///   (Level 0 leaks identity via the X-User-Id header regardless, but the
///   ownership pattern is established correctly for Level 1 JWT auth.)
/// </summary>
public sealed record DeleteFileCommand(
    /// <summary>Unique identifier of the file to delete.</summary>
    Guid FileId,

    /// <summary>Identity of the requesting user, used to verify file ownership.</summary>
    string UserId) : IRequest<Result>;
