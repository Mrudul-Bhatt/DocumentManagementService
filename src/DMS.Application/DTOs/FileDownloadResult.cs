namespace DMS.Application.DTOs;

/// <summary>
/// Carries the data required to stream a file download back to the HTTP caller.
///
/// Why a separate type from FileMetadataDto?
///   FileMetadataDto is a metadata-only projection (no file bytes) used by list and
///   upload responses. FileDownloadResult exists only for the download path, where the
///   caller needs the raw byte stream in addition to the content type and filename
///   needed to set response headers. Merging them would force every metadata response
///   to carry an open stream, which would be wasteful and semantically wrong.
///
/// Why carry a Stream instead of byte[]?
///   ASP.NET Core's File(stream, contentType, filename) helper pipes the stream
///   directly to the HTTP response body without reading it all into memory first.
///   This gives constant memory usage regardless of file size (up to the 25 MB cap).
///   A byte[] approach would load the entire file into the application's heap on
///   every download request — multiplied across concurrent users, this exhausts
///   memory under load.
///
/// Stream ownership and lifetime:
///   The stream is opened by the infrastructure layer (LocalFileStorageService) and
///   ownership is transferred to the controller via this DTO. ASP.NET Core closes
///   the stream automatically after the response has finished writing.
///   Neither the handler nor this DTO closes the stream.
///
/// Why a record?
///   Same reasoning as FileMetadataDto — immutable data carrier, constructed once
///   and consumed once. Value equality on Stream compares references, which is
///   acceptable here since this type is never compared or used in collections.
/// </summary>
public sealed record FileDownloadResult(
    /// <summary>
    /// Open, readable stream of the file's raw bytes.
    /// The caller (controller) must not close this stream — ASP.NET Core handles disposal
    /// after the response body has been written.
    /// </summary>
    Stream Content,

    /// <summary>
    /// Original filename, written into the Content-Disposition response header as:
    ///   Content-Disposition: attachment; filename="<Filename>"
    /// This tells the browser to save the file under this name rather than display it inline.
    /// </summary>
    string Filename,

    /// <summary>
    /// MIME type written into the Content-Type response header (e.g., "application/pdf").
    /// Tells the browser or HTTP client how to interpret the raw bytes in the response body.
    /// </summary>
    string MimeType);
