namespace DMS.Application.Services;

/// <summary>
/// Defines the contract for one-way password hashing and verification.
///
/// Why does this interface live in Application instead of Domain?
///   Password hashing is an infrastructure concern (it depends on a specific algorithm
///   library — BCrypt), but the Application layer handlers need to call it without
///   referencing Infrastructure. Defining it in Application keeps the dependency arrow
///   correct: Infrastructure implements it, Application uses it, Domain knows nothing of it.
///
/// Why one-way hashing instead of encryption?
///   Encryption is reversible — a compromised key exposes all passwords.
///   BCrypt is a one-way adaptive hash: it can never be reversed, and its cost factor
///   can be increased over time to keep pace with faster hardware. If the database is
///   breached, the attacker has hashes, not passwords, and cracking them is deliberately
///   slow (cost factor 12 = ~250ms per attempt on modern hardware).
///
/// Why a separate Verify method instead of hashing the input and comparing?
///   BCrypt embeds a random salt in every hash. Two hashes of the same password
///   produce different strings. Direct string comparison always fails — BCrypt.Verify()
///   extracts the salt from the stored hash and rehashes the candidate before comparing.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Produces a BCrypt hash of the given plaintext password.
    /// The returned string includes the salt and cost factor — it is safe to store
    /// directly in the database. The original password cannot be recovered from it.
    /// </summary>
    string Hash(string password);

    /// <summary>
    /// Returns true if the given plaintext password matches the stored BCrypt hash.
    /// Extracts the embedded salt from the hash before comparing — do not pre-hash
    /// the candidate before calling this method.
    /// </summary>
    bool Verify(string password, string hash);
}
