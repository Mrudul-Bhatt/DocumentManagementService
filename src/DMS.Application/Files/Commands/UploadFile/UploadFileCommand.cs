using DMS.Application.Common;
using DMS.Application.DTOs;
using MediatR;

namespace DMS.Application.Files.Commands.UploadFile;

/// <summary>
/// CQRS command that instructs the application to persist a new file on behalf of a user.
///
/// What is a Command (vs a Query)?
///   In CQRS, a Command represents an intent to change state. It carries all the data
///   needed to perform the write operation and returns a result confirming success or
///   describing the failure. Commands are dispatched via MediatR.Send() and handled
///   by exactly one IRequestHandler implementation.
///
/// Why a record?
///   Commands are immutable data carriers — once constructed they should not change.
///   Records enforce this with init-only properties and provide a compact positional
///   constructor. They also make the intent clear: this is a message, not an object
///   with behaviour.
///
/// Why sealed?
///   Commands are not designed for inheritance. sealed prevents accidental subclassing
///   and makes the type's purpose unambiguous.
///
/// Why pass Stream Content instead of byte[]?
///   The stream is opened by the controller directly from the HTTP request body
///   (IFormFile.OpenReadStream()). Passing it through here lets the handler pipe it
///   directly to storage without ever loading all file bytes into memory. A byte[]
///   parameter would require the controller to buffer the entire file first, defeating
///   the purpose of streaming.
/// </summary>
public sealed record UploadFileCommand(
    /// <summary>Identity of the user performing the upload. Becomes the file owner.</summary>
    string UserId,

    /// <summary>Original filename as provided by the caller (e.g., "report.pdf").</summary>
    string Filename,

    /// <summary>MIME type reported by the HTTP client (e.g., "application/pdf").</summary>
    string MimeType,

    /// <summary>
    /// File size in bytes as reported by the HTTP multipart form.
    /// Validated against the 25 MB cap before the stream is read,
    /// avoiding unnecessary I/O for oversized uploads.
    /// </summary>
    long FileSize,

    /// <summary>
    /// Raw readable stream of the file's bytes, sourced from the HTTP request body.
    /// Ownership is held by the handler for the duration of the upload operation.
    /// </summary>
    Stream Content) : IRequest<Result<FileMetadataDto>>;
