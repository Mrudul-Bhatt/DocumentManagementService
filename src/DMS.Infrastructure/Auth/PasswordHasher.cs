using DMS.Application.Services;

namespace DMS.Infrastructure.Auth;

/// <summary>
/// BCrypt.Net-Next implementation of IPasswordHasher.
///
/// Why BCrypt over SHA-256 or MD5?
///   BCrypt is specifically designed for password hashing. It is intentionally slow
///   (controlled by workFactor) and automatically generates and embeds a random salt.
///   Cryptographic hash functions (SHA-256, MD5) are designed to be fast, which makes
///   them easy to brute-force with GPU farms. BCrypt's cost factor means each attempt
///   takes ~250ms on modern hardware, making large-scale offline attacks impractical.
///
/// Why internal sealed?
///   This class is an infrastructure detail. No code outside DMS.Infrastructure should
///   reference it directly — consumers depend on the IPasswordHasher interface from the
///   Application layer. sealed prevents accidental subclassing inside Infrastructure.
///
/// Why no state? Why no constructor injection?
///   BCrypt is a pure algorithm call with no shared state. The workFactor is a constant
///   baked into every call. Stateless services like this are registered as Singleton in DI
///   (one instance for the process lifetime) — thread safety is automatic because there
///   is nothing to mutate.
/// </summary>
internal sealed class PasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Produces a BCrypt hash of the plaintext password with an embedded random salt.
    ///
    /// Why workFactor: 12?
    ///   workFactor is an exponent: BCrypt performs 2^workFactor rounds of key stretching.
    ///   At 12, hashing takes ~250ms on typical server hardware — slow enough to deter
    ///   brute-force attacks but fast enough for normal login throughput.
    ///   OWASP recommends a minimum of 10; 12 is the commonly chosen production value.
    ///   Higher values (13, 14) provide more resistance but increase login latency linearly.
    ///
    /// The returned string is a 60-character BCrypt hash in the $2a$ format, which includes
    /// the version identifier, workFactor, and the salt — no separate salt column is needed.
    /// </summary>
    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    /// <summary>
    /// Verifies a plaintext password against a stored BCrypt hash.
    ///
    /// Why is there a dedicated Verify() method instead of re-hashing and comparing?
    ///   BCrypt embeds the salt inside the hash string (the first 22 characters of the
    ///   $2a$... prefix). BCrypt.Verify() extracts that salt, hashes the candidate password
    ///   with it, and compares the results. Re-hashing with a new random salt would always
    ///   produce a different string and would never match, so direct string equality on
    ///   hash outputs is always wrong for BCrypt.
    ///
    /// Returns true if the password matches the hash; false otherwise.
    /// This call is the only place in the codebase that should read PasswordHash.
    /// </summary>
    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
