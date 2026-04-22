namespace DMS.Application.DTOs;

public sealed record FileDownloadResult(
    Stream Content,
    string Filename,
    string MimeType);
