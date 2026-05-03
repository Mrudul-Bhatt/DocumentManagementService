namespace DMS.Domain.Errors;

/// <summary>
/// Catalogue of all typed domain errors that the application can produce.
///
/// Why centralise errors here instead of throwing exceptions or using string messages?
///   1. Discoverability: every possible failure in the system is visible in one place.
///      A developer adding a new handler can see all existing errors and reuse them
///      rather than inventing ad-hoc strings.
///   2. Consistency: the API layer maps error Codes to HTTP status codes in
///      ResultExtensions.ErrorStatusCodes. A centralised catalogue means one mapping
///      entry per error — no duplication, no drift between the error raised and
///      the status code returned.
///   3. Stability: clients that branch on error codes (e.g., show "File not found"
///      vs "Permission denied") rely on those codes being stable strings. Defining
///      them as named constants here prevents accidental typo-driven changes.
///
/// Naming convention — "{Aggregate}.{ErrorName}":
///   The dot-separated prefix (File., User.) groups errors by the domain aggregate
///   they belong to. This mirrors the namespace structure and makes the Code value
///   self-explanatory in logs and API responses without needing additional context.
///
/// Why static readonly fields instead of const?
///   Error is a record (reference type), so it cannot be const. static readonly
///   allocates each Error instance once at class load — there is never more than
///   one instance of DomainErrors.File.NotFound in the process, which is correct
///   because these are identity values, not mutable state.
/// </summary>
public static class DomainErrors
{
    /// <summary>Errors related to file operations.</summary>
    public static class File
    {
        /// <summary>
        /// The requested file record does not exist in the database.
        /// Maps to HTTP 404 Not Found in ResultExtensions.
        /// </summary>
        public static readonly Error NotFound = new("File.NotFound", "The requested file does not exist.");

        /// <summary>
        /// The requesting user does not own the file.
        /// Maps to HTTP 403 Forbidden in ResultExtensions.
        /// Returned instead of NotFound to avoid leaking whether the file exists
        /// to a caller who should not have access — though at Level 0 (trusted
        /// X-User-Id header) this distinction has no real security value yet.
        /// </summary>
        public static readonly Error Forbidden = new("File.Forbidden", "You do not have permission to access this file.");

        /// <summary>
        /// The uploaded file exceeds the 25 MB size cap.
        /// Maps to HTTP 413 Request Entity Too Large in ResultExtensions.
        /// Checked in the handler as a defence-in-depth guard; the primary
        /// rejection happens earlier at the [RequestSizeLimit] controller attribute.
        /// </summary>
        public static readonly Error TooLarge = new("File.TooLarge", "File exceeds the maximum allowed size of 25 MB.");

        /// <summary>
        /// The uploaded file has a reported size of zero bytes.
        /// Maps to HTTP 400 Bad Request in ResultExtensions.
        /// </summary>
        public static readonly Error Empty = new("File.Empty", "Uploaded file cannot be empty.");
    }

    /// <summary>Errors related to caller identity.</summary>
    public static class User
    {
        /// <summary>
        /// The X-User-Id request header was absent or blank.
        /// Maps to HTTP 400 Bad Request in ResultExtensions.
        /// Level 0 only — replaced by JWT authentication in Level 1, at which
        /// point this error and the header are removed entirely.
        /// </summary>
        public static readonly Error IdMissing = new("User.IdMissing", "The X-User-Id header is required.");
    }
}

/// <summary>
/// Immutable value type representing a typed domain error.
///
/// Why a record?
///   Records provide value equality by default — two Error instances with the same
///   Code and Description are considered equal. This allows the Result base class
///   constructor to compare against Error.None using == without a custom Equals()
///   override, and makes error assertions in tests straightforward.
///
/// Why Code and Description as separate fields?
///   Code is a stable, machine-readable identifier (e.g., "File.NotFound") that
///   clients and the API mapping layer branch on — it must never change once published.
///   Description is a human-readable message for display — it can evolve without
///   breaking any client logic that keys off Code. Keeping them separate enforces
///   that distinction structurally.
///
/// Error.None:
///   Sentinel value representing "no error" on a successful Result. The Result
///   base constructor guards that a successful result always holds Error.None and
///   a failed result never does, maintaining the Result invariant.
/// </summary>
public record Error(string Code, string Description)
{
    /// <summary>
    /// Sentinel value used by successful Result instances.
    /// Code and Description are both empty strings — this value should never
    /// appear in a log or API response because it is only ever held by a
    /// Result where IsSuccess = true.
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
