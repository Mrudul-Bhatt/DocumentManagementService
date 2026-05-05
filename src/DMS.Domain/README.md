# DMS.Domain

The domain layer is the core of the entire system. It defines what the business is — its entities, rules, and the contracts it requires from the outside world. It has zero dependencies on any other layer and zero knowledge of HTTP, databases, or file systems. Every other layer depends on this one; this one depends on nothing.

---

## Position in the Architecture

```
┌─────────────┐
│   DMS.Api   │
└──────┬──────┘
       │
       ▼
┌──────────────────┐
│ DMS.Application  │
└──────┬───────────┘
       │
       ▼
┌──────────────────┐   ◄── this layer
│   DMS.Domain     │
│                  │   no outward arrows — depends on nothing
└──────────────────┘
```

This is the Dependency Inversion Principle in action. `DMS.Domain` defines interfaces (`IFileMetadataRepository`, `IUserRepository`, `IFileStorageService`, etc.) that describe what it needs. `DMS.Infrastructure` provides the concrete implementations. The domain never imports the infrastructure — the dependency arrow always points inward.

---

## Level 1 Changes from Level 0

| Area | Level 0 | Level 1 |
|---|---|---|
| Entities | `FileMetadata` | + `User`, `RefreshToken`, `AuditLog` |
| Enums | None | `Role` (`User`, `Admin`) |
| Error classes | `File` only | + `User` (4 errors), `Token` (2 errors) |
| Repositories | `IFileMetadataRepository` | + `IUserRepository`, `IRefreshTokenRepository`, `IAuditLogRepository` |
| Removed error | `User.IdMissing` (X-User-Id header) | Removed — replaced by JWT auth |

---

## Project Structure

```
DMS.Domain/
├── Entities/
│   ├── AuditLog.cs                    # Append-only audit trail entry
│   ├── FileMetadata.cs                # File ownership and location record
│   ├── RefreshToken.cs                # Opaque long-lived session token
│   └── User.cs                        # Authenticated user account
├── Enums/
│   └── Role.cs                        # User / Admin role classification
├── Errors/
│   └── DomainErrors.cs                # All named domain failures + the Error record
├── Repositories/
│   ├── IAuditLogRepository.cs         # Append-only audit log persistence contract
│   ├── IFileMetadataRepository.cs     # File metadata persistence contract
│   ├── IRefreshTokenRepository.cs     # Refresh token persistence contract
│   └── IUserRepository.cs             # User account persistence contract
└── Services/
    └── IFileStorageService.cs         # Binary file storage contract
```

---

## Entities

### `User`

```csharp
public sealed class User
{
    public Guid           Id           { get; private set; }
    public string         Email        { get; private set; }
    public string         PasswordHash { get; private set; }
    public Role           Role         { get; private set; }
    public bool           IsActive     { get; private set; }
    public DateTimeOffset CreatedAt    { get; private set; }
}
```

Represents an authenticated user account. All properties have `private set` — external code cannot reach in and mutate state directly. All state transitions must go through the methods defined on the class.

**`Create` factory method:**
```csharp
public static User Create(string email, string passwordHash, Role role = Role.User)
```
- Calls `email.ToLowerInvariant()` — normalises at write time so all downstream comparisons and the unique index work correctly without case-insensitive collation.
- Sets `IsActive = true` — all new accounts are active by default.
- Generates `Id = Guid.NewGuid()` — domain controls its own identity.

**Domain methods:**
```csharp
public void Suspend()  => IsActive = false;
public void Activate() => IsActive = true;
```
Account status transitions are expressed as named domain methods, not as property assignments from outside the class.

**`IsActive` and the login check ordering:**
The `LoginCommandHandler` checks `IsActive` *after* verifying the password. This is deliberate — returning `User.Suspended` before checking credentials would reveal that a suspended account exists (even when the wrong password is given), leaking account presence to an attacker.

**Why `PasswordHash` is stored on the entity but never on DTOs:**
The entity holds the hash because it needs it to verify the password via `IPasswordHasher.Verify()`. The hash is never included in any DTO returned to the API layer — the DTO boundary is the structural enforcement of this rule.

---

### `RefreshToken`

```csharp
public sealed class RefreshToken
{
    public Guid            Id        { get; private set; }
    public Guid            UserId    { get; private set; }
    public string          Token     { get; private set; }
    public DateTimeOffset  ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset  CreatedAt { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive  => !IsExpired && !IsRevoked;
}
```

Represents a long-lived opaque session token used to obtain new access tokens without re-entering credentials.

**Why opaque string instead of JWT?**
JWTs are stateless — the server cannot revoke one without maintaining a denylist. Opaque tokens are looked up in the database, so revocation is instant: mark the row and the token is dead on the next use. Refresh tokens must support explicit logout, making statefulness necessary.

**Computed properties (`IsExpired`, `IsRevoked`, `IsActive`):**
These are evaluated at access time against `DateTimeOffset.UtcNow` — they are never stale. `IsActive` is the single gate that both the Refresh and Revoke handlers check before proceeding.

**Why `RevokedAt` is a nullable `DateTimeOffset` (not a boolean):**
The timestamp records *when* revocation happened, which is valuable for forensics ("show me all tokens revoked in the last hour"). A boolean would discard that information. The computed `IsRevoked` property derives the boolean view from the nullable timestamp.

**`Revoke` method:**
```csharp
public void Revoke() => RevokedAt = DateTimeOffset.UtcNow;
```
The token is not deleted — the row is retained for audit. The caller must persist the mutation via `IRefreshTokenRepository.UpdateAsync()`.

**Rotation model:**
Every call to the Refresh endpoint revokes the presented token and issues a new one. If a token is stolen and used by an attacker first, the legitimate owner's next refresh attempt finds their token already revoked — a detectable theft signal.

---

### `AuditLog`

```csharp
public sealed class AuditLog
{
    public Guid            Id           { get; private set; }
    public string          UserId       { get; private set; }
    public string          Action       { get; private set; }   // e.g. "File.Upload"
    public string          ResourceType { get; private set; }   // e.g. "File"
    public string?         ResourceId   { get; private set; }   // e.g. the file's GUID
    public string?         IpAddress    { get; private set; }
    public DateTimeOffset  OccurredAt   { get; private set; }
}
```

An immutable record of a security-relevant action. Written by `AuditLoggingBehaviour` after every successful `IAuditableRequest` handler.

**Why append-only?**
An audit log exists to provide a tamper-evident trail. If records could be modified or deleted after the fact, the log would lose its trustworthiness as evidence. `IAuditLogRepository` exposes only `AddAsync` — there is no pathway in the application code to mutate an existing entry.

**Why `OccurredAt` (not `CreatedAt`)?**
The name reflects domain meaning: this is when the auditable event happened. `OccurredAt` is set inside `AuditLog.Create()` — callers cannot pass an arbitrary timestamp, preventing backdating.

**Why `ResourceId` and `IpAddress` are nullable:**
- `ResourceId` is null for collection-scoped operations (`File.List`) and for `File.Upload` (the file GUID is generated inside the handler, after the audit record's fields are bound at command construction time).
- `IpAddress` is null when unavailable behind certain reverse proxies.

**Why no FK constraint to Users:**
Audit records must outlive the user who created them (for compliance and forensics). If the user is ever deleted, the audit history must remain intact. The `UserId` is stored as a plain string rather than a navigable foreign key.

---

### `FileMetadata`

```csharp
public sealed class FileMetadata
{
    public Guid           Id          { get; private set; }
    public string         UserId      { get; private set; }
    public string         Filename    { get; private set; }
    public long           FileSize    { get; private set; }
    public string         MimeType    { get; private set; }
    public string         StoragePath { get; private set; }
    public DateTimeOffset UploadedAt  { get; private set; }
}
```

Records the metadata of an uploaded file. The file content lives in the storage backend; `StoragePath` is the opaque pointer to it.

**Why no file bytes on the entity?**
Loading binary content whenever the entity is fetched would cause every metadata query to pull potentially megabytes of data into memory — even when only the filename or MIME type is needed. Separating metadata from content is the correct model.

**`Create` factory method:**
`StoragePath` is passed in from `IFileStorageService.SaveAsync()` — which runs first inside the command handler. The entity is created only *after* the bytes are safely on disk, preventing orphaned metadata rows in the event of a storage failure.

**`BelongsTo` — ownership check as a domain method:**
```csharp
public bool BelongsTo(string userId) => UserId == userId;
```
The ownership rule lives on the entity that owns the data it is about. Handlers call `file.BelongsTo(userId)` rather than comparing `file.UserId == userId` directly — if the ownership model ever changes (e.g. team-owned files), there is exactly one place to update.

---

## Enums

### `Role`

```csharp
public enum Role
{
    User,   // = 0  (default)
    Admin
}
```

Authorization role for user accounts. Stored on `User` and embedded as a claim in the JWT at login time. ASP.NET Core's `[Authorize(Roles = "Admin")]` reads this claim.

**Why `User = 0` (the default)?**
If an enum value is ever uninitialised through a bug or migration error, it falls to `User` rather than `Admin`. The privileged role should never be the zero value — fail closed, not open.

**Why store as string in the database?**
`UserConfiguration` uses `HasConversion<string>()`. Integer storage is brittle: inserting a new role before `Admin` in the enum declaration would silently shift all stored ordinals. String storage is self-documenting and survives enum reordering.

---

## `DomainErrors` — Named Error Catalogue

All named failures are defined here as static readonly fields — a single catalogue of every error the domain can produce.

```
DomainErrors
├── File
│   ├── NotFound          →  404
│   ├── Forbidden         →  403
│   ├── TooLarge          →  413
│   └── Empty             →  400
├── User
│   ├── NotFound          →  404
│   ├── EmailAlreadyExists →  409
│   ├── InvalidCredentials →  401
│   └── Suspended         →  403
└── Token
    ├── Invalid           →  401
    └── Expired           →  401
```

**Why a central catalogue?**
Errors are referenced by multiple layers — handlers return them; `ResultExtensions` maps them to HTTP status codes. A central definition means one change propagates everywhere automatically.

**Why `static readonly` and not `const`?**
`const` is restricted to primitive types. `Error` is a record (reference type) — `static readonly` is the correct approach for a reference-type constant allocated once at startup.

### Notable error design decisions

**`User.InvalidCredentials` for both "email not found" and "wrong password":**
A single error prevents user enumeration — an attacker cannot determine whether an account exists by comparing error codes. The login handler also hashes the provided password even when the user is not found (constant-time response) to eliminate timing-based enumeration.

**`User.Suspended` returned only after credential verification:**
If the suspended check came before the password check and returned a distinct error code, an attacker with a wrong password could still detect account existence by receiving `Suspended` instead of `InvalidCredentials`.

**`Token.Invalid` vs `Token.Expired`:**
Kept separate so the client can display "session expired, please log in again" (from `Token.Expired`) rather than a generic "token invalid" message. Both map to 401, but the descriptions differ.

### `Error` Record

```csharp
public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
```

Record value equality means two `Error` instances with the same `Code` and `Description` are equal — used by `Result`'s constructor guard to detect misuse (e.g., constructing a success result with a non-None error).

`Error.None` is the sentinel representing "no error", used only on the success path of a `Result`. It is defined on `Error` itself, not inside `DomainErrors`, because it is the absence of a domain failure — not a failure itself.

---

## Repository Interfaces

All repository interfaces are defined here in the Domain layer to enforce the Dependency Inversion Principle — Application handlers depend on these contracts, not on EF Core or SQL Server. Implementations live exclusively in `DMS.Infrastructure`.

### `IFileMetadataRepository`

```csharp
Task<FileMetadata?>              GetByIdAsync(Guid id, CancellationToken ct = default);
Task<IReadOnlyList<FileMetadata>> GetByUserIdAsync(string userId, CancellationToken ct = default);
Task                             AddAsync(FileMetadata file, CancellationToken ct = default);
Task                             DeleteAsync(FileMetadata file, CancellationToken ct = default);
```

No `UpdateAsync` — `FileMetadata` is immutable after upload. No properties change once the row is written.

`DeleteAsync` accepts the entity (not just the GUID) because the handler already holds the tracked instance (fetched for the `BelongsTo` check). Passing the entity avoids a redundant database lookup inside the repository.

`IReadOnlyList<T>` on `GetByUserIdAsync` guarantees the result is fully materialised before the method returns. `IEnumerable<T>` would leave deferred execution open — the database call could happen after the DbContext is disposed.

### `IUserRepository`

```csharp
Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
Task        AddAsync(User user, CancellationToken ct = default);
Task        UpdateAsync(User user, CancellationToken ct = default);
```

`GetByEmailAsync` is the login key — runs on every authentication attempt against the unique `IX_Users_Email` index.

`UpdateAsync` exists to persist mutations from `Suspend()` / `Activate()`. No `DeleteAsync` — users are soft-deleted via `Suspend()`, preserving referential integrity with `AuditLog` records.

### `IRefreshTokenRepository`

```csharp
Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
Task                AddAsync(RefreshToken refreshToken, CancellationToken ct = default);
Task                UpdateAsync(RefreshToken refreshToken, CancellationToken ct = default);
```

`GetByTokenAsync` — lookup by the opaque token string (the unique `IX_RefreshTokens_Token` index). The client sends the token string; the associated `UserId` is read from the database row, not from the request. This prevents a caller from claiming ownership of another user's token.

No `GetByIdAsync` — application code never looks up a refresh token by its GUID primary key.

No `DeleteAsync` — revoked and expired tokens are retained for forensic analysis. Soft-revocation via `Revoke()` + `UpdateAsync()` sets `RevokedAt`; the row persists as an audit trail.

### `IAuditLogRepository`

```csharp
Task AddAsync(AuditLog log, CancellationToken ct = default);
```

A single-method interface by design. The append-only constraint is structural — no `UpdateAsync`, no `DeleteAsync`, no `GetBy*` methods exist to call. Adding a query method would be done only when an admin audit viewer endpoint is implemented.

Callers (`AuditLoggingBehaviour`) wrap this call in a try/catch that swallows exceptions — audit failure must not propagate as a 500 error to the client.

---

## `IFileStorageService` — Storage Contract

```csharp
Task<string> SaveAsync(Stream content, string userId, string fileId, CancellationToken ct = default);
Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default);
Task         DeleteAsync(string storagePath, CancellationToken ct = default);
```

A Domain Service interface — it represents a capability the domain requires that cannot be modelled as a method on an entity. Storing raw bytes is an infrastructure concern; declaring the need for it is a domain concern.

**`SaveAsync` returns a `string` path:**
The storage service owns path construction. Application code passes `userId` and `fileId` as inputs; the implementation decides the physical layout (e.g. `uploads/{userId}/{fileId}`). The returned `storagePath` is stored in `FileMetadata` and passed back for future reads and deletes. The domain treats it as an opaque handle.

**`ReadAsync` throws `FileNotFoundException` (not `Result.Failure`):**
A missing physical file when the metadata row exists is a data integrity violation — the metadata and storage are out of sync. This is a programming or operational error, not an expected business condition. The exception surfaces it clearly for investigation rather than silently returning a "not found" to the client.

**`DeleteAsync` is idempotent:**
The delete flow removes the physical file first, then the metadata row. If the process crashes between the two steps and is retried, `DeleteAsync` is called on a path that no longer exists. An idempotent implementation (guard with `File.Exists`) makes the retry safe.

---

## Design Philosophy — Why the Domain Has No Dependencies

The domain layer imports no NuGet packages beyond the .NET base class library. This is not accidental — it is the core principle of Clean Architecture.

- **Testability:** Domain logic can be tested with plain unit tests. No database, no HTTP, no file system needed.
- **Longevity:** Infrastructure frameworks change (ORMs, cloud SDKs, HTTP libraries). The domain model describes the business, which changes far more slowly. Keeping them separate means framework upgrades do not require rewriting business logic.
- **Clarity:** When a developer opens `DMS.Domain`, they see only business concepts — entities, rules, errors, contracts. There is no noise from persistence annotations, HTTP attributes, or serialisation concerns.

The dependency rule is: source code dependencies always point inward. The domain is the innermost layer and depends on nothing outside itself.
