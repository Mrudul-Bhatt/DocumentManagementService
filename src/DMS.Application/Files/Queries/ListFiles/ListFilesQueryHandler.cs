using DMS.Application.Common;
using DMS.Application.DTOs;
using DMS.Domain.Repositories;
using MediatR;

namespace DMS.Application.Files.Queries.ListFiles;

/// <summary>
/// Handles the ListFilesQuery: fetches all file metadata records for a user
/// and projects them to DTOs.
/// </summary>
internal sealed class ListFilesQueryHandler(IFileMetadataRepository repository)
    : IRequestHandler<ListFilesQuery, Result<IReadOnlyList<FileMetadataDto>>>
{
    /// <summary>
    /// Executes the list operation.
    ///
    /// Flow:
    ///   1. Fetch all FileMetadata records for the given UserId from the database.
    ///   2. Project each entity to a FileMetadataDto (no domain entity leaves this layer).
    ///   3. Wrap the read-only list in a successful Result and return.
    ///
    /// Why always return Result.Success here (never a failure)?
    ///   An empty file list is a valid, expected state — a user with no uploads should
    ///   receive 200 OK with an empty array, not a 404. There is no domain error
    ///   that can occur during a list operation for an authenticated user, so the
    ///   Result wrapper is present purely for interface consistency with other handlers.
    ///
    /// Why .ToList().AsReadOnly() instead of returning the IEnumerable directly?
    ///   .ToList() materialises the LINQ projection immediately, ensuring the database
    ///   query executes inside this handler (not deferred until the controller serialises
    ///   the response). .AsReadOnly() wraps the List<T> in a ReadOnlyCollection<T>,
    ///   preventing callers from casting back to List<T> and mutating the collection.
    ///
    /// Why explicit type argument on Result.Success<IReadOnlyList<FileMetadataDto>>()?
    ///   The compiler cannot infer IReadOnlyList<T> from a ReadOnlyCollection<T> argument
    ///   because the generic parameter in Result.Success<TValue> would resolve to the
    ///   concrete type. The explicit argument pins the Result's value type to the
    ///   interface, matching the handler's declared return type.
    /// </summary>
    public async Task<Result<IReadOnlyList<FileMetadataDto>>> Handle(ListFilesQuery query, CancellationToken ct)
    {
        var files = await repository.GetByUserIdAsync(query.UserId, ct);

        var dtos = files
            .Select(f => new FileMetadataDto(f.Id, f.Filename, f.FileSize, f.MimeType, f.UploadedAt))
            .ToList()
            .AsReadOnly();

        return Result.Success<IReadOnlyList<FileMetadataDto>>(dtos);
    }
}
