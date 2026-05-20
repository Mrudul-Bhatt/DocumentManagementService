namespace DMS.Domain.Errors;

/// <summary>
/// Centralised catalogue of all domain errors.
///
/// Why a static class of nested static classes?
///   Errors are grouped by the domain concept they belong to (File, User, Token).
///   This mirrors the namespace of the entity that owns the rule, making it easy to
///   find errors at the call site: "DomainErrors.User.NotFound" reads like a sentence.
///   The nesting also prevents name collisions — File.NotFound and User.NotFound are
///   distinct types that compile without ambiguity.
///
/// Why static readonly fields instead of const?
///   Error is a record (reference type). const is restricted to primitive types
///   (int, string, etc.) and cannot hold a record instance. static readonly is the
///   correct approach for reference-type constants that should be allocated once.
///
/// Why not throw exceptions?
///   Domain errors are expected outcomes, not programming mistakes. The Result pattern
///   (Result and Result<T>) carries errors as values through the call chain. This keeps
///   the happy path readable, avoids exception overhead for business-logic failures, and
///   forces callers to explicitly handle the error case.
/// </summary>
public static class DomainErrors
{
    /// <summary>Errors related to file operations.</summary>
    public static class File
    {
        /// <summary>Returned when GetByIdAsync returns null — the file GUID does not exist in the database.</summary>
        public static readonly Error NotFound = new("File.NotFound", "The requested file does not exist.");

        /// <summary>
        /// Returned when the authenticated user's ID does not match the file's UserId.
        /// The handler returns 403 (not 404) for wrong-owner access because the file
        /// does exist — returning 404 would be misleading. The distinction between
        /// "not found" and "forbidden" is intentional here because there is no user
        /// enumeration risk: the attacker already needs the file's GUID.
        /// </summary>
        public static readonly Error Forbidden = new("File.Forbidden", "You do not have permission to access this file.");

        /// <summary>Returned when the uploaded file's declared size exceeds the 25 MB cap. Checked before the stream is read to avoid wasting bandwidth.</summary>
        public static readonly Error TooLarge = new("File.TooLarge", "File exceeds the maximum allowed size of 25 MB.");

        /// <summary>Returned when the uploaded file's declared size is zero bytes.</summary>
        public static readonly Error Empty = new("File.Empty", "Uploaded file cannot be empty.");
    }

    /// <summary>Errors related to user account operations.</summary>
    public static class User
    {
        /// <summary>Returned when a user lookup by ID fails. Distinct from InvalidCredentials so internal flows can distinguish "user deleted mid-session" from "wrong password."</summary>
        public static readonly Error NotFound = new("User.NotFound", "The requested user does not exist.");

        /// <summary>Returned during registration when the email is already in the database (unique index violation caught before insert).</summary>
        public static readonly Error EmailAlreadyExists = new("User.EmailAlreadyExists", "An account with this email address already exists.");

        /// <summary>
        /// Returned for both "email not found" and "password wrong" during login.
        ///
        /// Why the same error for both cases?
        ///   User enumeration prevention: if "email not found" returned 404 and "wrong password"
        ///   returned 401, an attacker could determine whether any email is registered by observing
        ///   the response code. A single, vague error for both cases prevents this information leak.
        ///   The login handler always hashes the provided password (even when the user is not found)
        ///   to prevent timing-based enumeration as well.
        /// </summary>
        public static readonly Error InvalidCredentials = new("User.InvalidCredentials", "The email or password is incorrect.");

        /// <summary>
        /// Returned after credentials are verified but IsActive is false. The credential
        /// check happens first so an attacker with wrong credentials still gets InvalidCredentials
        /// (401) rather than Suspended (403), which would reveal that the account exists.
        /// </summary>
        public static readonly Error Suspended = new("User.Suspended", "This account has been suspended.");
    }

    /// <summary>Errors related to folder operations.</summary>
    public static class Folder
    {
        public static readonly Error NotFound        = new("Folder.NotFound",        "The requested folder does not exist.");
        public static readonly Error Forbidden       = new("Folder.Forbidden",       "You do not have permission to access this folder.");
        public static readonly Error MaxDepthExceeded = new("Folder.MaxDepthExceeded", "Folders cannot be nested more than 20 levels deep.");
        public static readonly Error NameConflict    = new("Folder.NameConflict",    "A folder with this name already exists in the same location.");
    }

    /// <summary>Errors related to file version operations.</summary>
    public static class FileVersion
    {
        public static readonly Error NotFound              = new("FileVersion.NotFound",              "The requested file version does not exist.");
        public static readonly Error CannotDeleteCurrent   = new("FileVersion.CannotDeleteCurrent",   "The current version of a file cannot be deleted. Delete the file instead.");
    }

    /// <summary>Errors related to direct user-to-user share grants.</summary>
    public static class Share
    {
        public static readonly Error NotFound            = new("Share.NotFound",            "The requested share does not exist.");
        public static readonly Error AlreadyExists       = new("Share.AlreadyExists",       "This resource is already shared with that user.");
        public static readonly Error MaxSharesExceeded   = new("Share.MaxSharesExceeded",   "A resource cannot have more than 500 shares.");
        public static readonly Error CannotShareWithSelf = new("Share.CannotShareWithSelf", "You cannot share a resource with yourself.");
    }

    /// <summary>Errors related to token-based public links.</summary>
    public static class PublicLink
    {
        public static readonly Error NotFound        = new("PublicLink.NotFound",        "The public link does not exist or has been revoked.");
        public static readonly Error Expired         = new("PublicLink.Expired",         "This public link has expired.");
        public static readonly Error InvalidPassword = new("PublicLink.InvalidPassword", "The password provided for this link is incorrect.");
    }

    /// <summary>Errors related to JWT access tokens and opaque refresh tokens.</summary>
    public static class Token
    {
        /// <summary>
        /// Returned when a refresh token string is not found in the database, is already
        /// revoked, or is expired. A single error code for all three states prevents a client
        /// (or attacker) from determining the exact failure reason — all three outcomes require
        /// the same client action: re-authenticate from scratch.
        /// </summary>
        public static readonly Error Invalid = new("Token.Invalid", "The token is invalid.");

        /// <summary>
        /// Returned specifically when an expired-but-otherwise-valid refresh token is presented.
        /// Kept separate from Invalid so the client can display a "session expired, please log in"
        /// message rather than a generic "token invalid" message to the user.
        /// </summary>
        public static readonly Error Expired = new("Token.Expired", "The token has expired.");
    }
}

/// <summary>
/// Value object representing a typed domain error.
///
/// Why a record?
///   Records have structural equality by default — two Error instances with the same
///   Code and Description are considered equal. This makes comparing errors in unit tests
///   and handler logic straightforward without manually implementing Equals/GetHashCode.
///
/// Why Code + Description?
///   Code is a machine-readable identifier for ResultExtensions to map to an HTTP status.
///   Description is a human-readable message safe to surface to the API caller.
///
/// Error.None is the sentinel "no error" value. Result.Success() sets Error to None;
///   handlers check Result.IsFailure (i.e., Error != None) to determine if the operation
///   failed. This avoids null checks throughout the codebase.
/// </summary>
public record Error(string Code, string Description)
{
    /// <summary>Sentinel value representing the absence of an error. Used by Result.Success().</summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
