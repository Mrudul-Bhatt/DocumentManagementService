namespace DMS.Domain.Enums;

/// <summary>
/// Authorization roles for user accounts.
///
/// Why an enum in the Domain layer?
///   Role is a business concept that governs what a user is permitted to do — it belongs
///   in the domain, not in the API or infrastructure. Keeping it here means all layers
///   can reference the canonical type without introducing a cross-layer dependency on a
///   higher layer.
///
/// How is it used?
///   Stored on the User entity and embedded in the JWT as a claim (ClaimTypes.Role) at
///   login time. ASP.NET Core's [Authorize(Roles = "Admin")] reads this claim to enforce
///   role-based access at the controller action level (wired in a future level).
///
/// Why User = 0 (implicit)?
///   C# enums default to 0 for the first member. User is the safe default — if an enum
///   value is somehow uninitialised (e.g., a data migration bug), it falls to User rather
///   than Admin, failing closed rather than open. Never make a privileged role the zero value.
/// </summary>
public enum Role
{
    /// <summary>Standard authenticated user. Can upload, download, list, and delete their own files.</summary>
    User,

    /// <summary>
    /// Privileged administrator. Intended for future endpoints that operate across all users
    /// (e.g., list all files, suspend accounts). No Admin-only endpoints exist in Level 1.
    /// </summary>
    Admin
}
