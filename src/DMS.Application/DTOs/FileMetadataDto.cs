namespace DMS.Application.DTOs;

/// <summary>
/// Read-only projection of a file's metadata returned to the API layer.
///
/// Why a DTO instead of exposing the FileMetadata domain entity directly?
///   Domain entities carry behaviour (factory methods, domain rules, EF Core
///   navigation properties) and internal state that the API consumer has no
///   business seeing. A DTO is a flat, serialisation-safe snapshot that contains
///   only the fields the caller needs. This also decouples the API contract from
///   the domain model — the entity can evolve (new fields, renamed properties,
///   changed types) without breaking the response shape clients depend on.
///
/// Why a record?
///   Records provide structural equality, a compact positional constructor, and
///   immutability by default — all desirable for a read-only data carrier that
///   is constructed once, serialised, and discarded. No setters means the DTO
///   cannot be accidentally mutated after projection.
///
/// Why sealed?
///   DTOs are not designed to be inherited. sealed signals this explicitly and
///   allows the runtime to make minor dispatch optimisations.
///
/// Used by:
///   - UploadFileCommandHandler  → returned in Result on success (201 Created body)
///   - ListFilesQueryHandler     → each element in the IReadOnlyList response (200 OK body)
/// </summary>
public sealed record FileMetadataDto(
    /// <summary>Unique identifier of the file. Used in the download/delete URL: /api/files/{Id}</summary>
    Guid Id,

    /// <summary>Original filename as supplied by the caller at upload time.</summary>
    string Filename,

    /// <summary>File size in bytes. Clients can display this without re-requesting the file.</summary>
    long FileSize,

    /// <summary>MIME type (e.g., "application/pdf"). Set by the caller and stored verbatim.</summary>
    string MimeType,

    /// <summary>
    /// UTC timestamp of when the file was uploaded.
    /// DateTimeOffset is used instead of DateTime to carry explicit timezone information —
    /// the offset is always +00:00 (UTC) here, but the type makes that unambiguous to
    /// serialisers and consumers rather than relying on an undocumented convention.
    /// </summary>
    DateTimeOffset UploadedAt);
