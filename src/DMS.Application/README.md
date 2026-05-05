# DMS.Application

The application layer is the brain of the service. It orchestrates use cases — deciding what needs to happen, in what order, and whether it succeeded or failed. It contains no HTTP concerns (those belong to `DMS.Api`) and no persistence or storage mechanics (those belong to `DMS.Infrastructure`). It depends only on `DMS.Domain` for entities, repository interfaces, and domain errors.

---

## Position in the Architecture

```
┌─────────────┐
│   DMS.Api   │  sends Commands / Queries via MediatR
└──────┬──────┘
       │
       ▼
┌──────────────────┐   ◄── this layer
│ DMS.Application  │
└──────┬───────────┘
       │  calls interfaces defined in DMS.Domain
       ▼
┌──────────────────┐        ┌──────────────────────┐
│   DMS.Domain     │        │  DMS.Infrastructure  │
│  (interfaces)    │◄───────│  (implementations)   │
└──────────────────┘        └──────────────────────┘
```

`DMS.Application` defines what it needs (`IFileMetadataRepository`, `IFileStorageService`, `IPasswordHasher`, `IJwtTokenService`, etc.) but never knows how those are implemented. The DI container wires the concrete implementations at startup.

---

## Level 1 Changes from Level 0

| Area | Level 0 | Level 1 |
|---|---|---|
| Auth commands | None | Register, Login, RefreshToken, RevokeToken |
| Service interfaces | None | `IPasswordHasher`, `IJwtTokenService`, `IEmailService` |
| Settings | None | `JwtSettings` (Options pattern) |
| Cross-cutting | None | `IAuditableRequest` + `AuditLoggingBehaviour` pipeline behaviour |
| New DTOs | — | `AuthTokensDto`, `UserDto` |
| File commands/queries | No `IpAddress` | `IpAddress` parameter + `IAuditableRequest` on all 4 |
| DependencyInjection | MediatR only | + open-generic `AuditLoggingBehaviour<,>` registration |

---

## Project Structure

```
DMS.Application/
├── Auth/
│   └── Commands/
│       ├── Register/
│       │   ├── RegisterCommand.cs              # Input: email, password
│       │   └── RegisterCommandHandler.cs       # Hash password, create user, issue tokens
│       ├── Login/
│       │   ├── LoginCommand.cs                 # Input: email, password, IP
│       │   └── LoginCommandHandler.cs          # Verify credentials, check active, issue tokens
│       ├── RefreshToken/
│       │   ├── RefreshTokenCommand.cs          # Input: refresh token string
│       │   └── RefreshTokenCommandHandler.cs   # Validate, rotate token, issue new pair
│       └── RevokeToken/
│           ├── RevokeTokenCommand.cs           # Input: refresh token string
│           └── RevokeTokenCommandHandler.cs    # Mark token as revoked (explicit logout)
├── Behaviours/
│   └── AuditLoggingBehaviour.cs               # Pipeline behaviour: writes AuditLog after success
├── Common/
│   ├── IAuditableRequest.cs                   # Marker interface: opt-in to audit logging
│   └── Result.cs                              # Result / Result<T> — typed outcome wrapper
├── DTOs/
│   ├── AuthTokensDto.cs                       # Access token + refresh token + expiry
│   ├── FileDownloadResult.cs                  # File stream + metadata for download
│   ├── FileMetadataDto.cs                     # Read model for file metadata
│   └── UserDto.cs                             # Public user data (no password hash)
├── Files/
│   ├── Commands/
│   │   ├── UploadFile/
│   │   │   ├── UploadFileCommand.cs           # + IpAddress, implements IAuditableRequest
│   │   │   └── UploadFileCommandHandler.cs
│   │   └── DeleteFile/
│   │       ├── DeleteFileCommand.cs           # + IpAddress, implements IAuditableRequest
│   │       └── DeleteFileCommandHandler.cs
│   └── Queries/
│       ├── ListFiles/
│       │   ├── ListFilesQuery.cs              # + IpAddress, implements IAuditableRequest
│       │   └── ListFilesQueryHandler.cs
│       └── DownloadFile/
│           ├── DownloadFileQuery.cs           # + IpAddress, implements IAuditableRequest
│           └── DownloadFileQueryHandler.cs
├── Services/
│   ├── IEmailService.cs                       # Send password reset email (stub in Level 1)
│   ├── IJwtTokenService.cs                    # Generate access token + refresh token
│   └── IPasswordHasher.cs                     # BCrypt hash and verify
├── Settings/
│   └── JwtSettings.cs                         # Options pattern: JWT config contract
└── DependencyInjection.cs                     # MediatR + AuditLoggingBehaviour registration
```

---

## Core Patterns

### CQRS — Command Query Responsibility Segregation

Every operation is classified as either a **Command** (mutates state) or a **Query** (reads state, never mutates).

| Type | Operations | Returns |
|---|---|---|
| Command | Register, Login, RefreshToken, RevokeToken, Upload, Delete | `Result` / `Result<T>` |
| Query | ListFiles, DownloadFile | `Result<T>` (read model) |

Note: `RefreshToken` is a Command even though it returns a token pair — it mutates state by revoking the old token and persisting the new one. Side effects classify an operation as a Command regardless of what it returns.

### Mediator Pattern (via MediatR)

The API layer sends a record to MediatR (`ISender.Send(command)`), which resolves and invokes the correct `IRequestHandler`. The controller never references the handler class — the contract is the command/query record.

**Pipeline behaviours** (`IPipelineBehavior<TRequest, TResponse>`) insert cross-cutting logic between the sender and the handler without modifying either. Level 1 adds `AuditLoggingBehaviour` this way.

### Result Pattern

All operations return `Result` or `Result<TValue>` instead of throwing exceptions for expected failures.

```
Success path:  Result.Success(value)   →  IsSuccess = true,  Value = value
Failure path:  Result.Failure(error)   →  IsFailure = true,  Error = { Code, Description }
```

`Result<TValue>.Value` throws `InvalidOperationException` if accessed on a failed result — a deliberate guard that forces callers to check `IsFailure` first.

---

## Pipeline Behaviour — `AuditLoggingBehaviour`

```csharp
public sealed class AuditLoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuditableRequest
```

An `IPipelineBehavior` wraps every MediatR handler invocation. `AuditLoggingBehaviour` runs **after** the handler succeeds and writes an `AuditLog` record to the database.

**Execution sequence:**
```
MediatR.Send(command)
    → AuditLoggingBehaviour.Handle()
        → await next()              ← handler runs here
        → if result.IsSuccess:
            AuditLog.Create(...)
            IAuditLogRepository.AddAsync(...)
```

**Key design decisions:**
- **Opt-in via `IAuditableRequest`**: only requests implementing the marker interface are audited. The `where TRequest : IAuditableRequest` constraint means the behaviour is not even registered for non-auditable requests.
- **Post-success only**: audit records are only written on success. A failed operation leaves no audit trail entry — the handler's own error logging covers failures.
- **Failure is swallowed**: the audit write is wrapped in a try/catch. If the database write fails (transient error), the primary response still reaches the client. Audit failure is logged as a warning but does not break the operation.

### `IAuditableRequest` — Marker Interface

```csharp
public interface IAuditableRequest
{
    string Action        { get; }  // e.g. "File.Upload"
    string ResourceType  { get; }  // e.g. "File"
    string? ResourceId   { get; }  // e.g. the file's GUID; null for collection ops
    string? IpAddress    { get; }  // caller's IP from HttpContext
}
```

A marker interface that opts a command or query into audit logging. `ResourceId` is nullable because:
- Collection-scoped operations (`File.List`) have no single resource.
- `File.Upload` does not know the file's GUID at command construction time (the GUID is generated inside the handler).

All four file operations implement `IAuditableRequest`. The auth commands (Register, Login) do not — authentication events are handled by separate security logging rather than the general audit trail.

---

## Auth Commands

### `RegisterCommand` / `RegisterCommandHandler`

**Input:** `{ Email, Password }`

**Flow:**
1. Check for existing account with the same email — return `User.EmailAlreadyExists` if found.
2. Hash the password via `IPasswordHasher.Hash()` (BCrypt, cost 12).
3. Create the `User` domain entity via `User.Create()`.
4. Persist the user via `IUserRepository.AddAsync()`.
5. Issue an access token + refresh token pair via `IssueTokenPairAsync()`.
6. Persist the `RefreshToken` entity.
7. Return `AuthTokensDto` on success.

**Why tokens on registration?**
Issuing tokens immediately means the user is logged in as soon as they register — no separate login step required. This is the expected UX in modern applications.

### `LoginCommandHandler`

**Input:** `{ Email, Password, IpAddress }`

**Flow:**
1. Look up the user by email.
2. If not found: **still hash** the provided password before returning `InvalidCredentials`.
3. Verify the BCrypt hash — return `InvalidCredentials` if wrong.
4. Check `user.IsActive` — return `User.Suspended` if false.
5. Issue token pair, persist refresh token.
6. Return `AuthTokensDto`.

**User enumeration prevention:**
A single `User.InvalidCredentials` error is returned for both "email not found" and "wrong password". The handler also hashes the password even when the user doesn't exist (step 2) — this eliminates the timing difference between the two cases that an attacker could measure to detect account existence.

**Why `IsActive` is checked after credential verification?**
If a suspended account check came before the password check and returned a distinct error, an attacker could determine whether any email belongs to an active account by sending a wrong password and comparing error codes. Credential check first ensures `User.Suspended` (403) is only returned to someone who already knows the correct password.

### `RefreshTokenCommand` / `RefreshTokenCommandHandler`

**Input:** `{ Token }` (the opaque refresh token string)

**Flow:**
1. Look up the token by its string value via `IRefreshTokenRepository.GetByTokenAsync()`.
2. Return `Token.Invalid` if not found or `!token.IsActive` (covers both expired and revoked).
3. Load the associated user — return `User.NotFound` if the account was deleted.
4. Check `user.IsActive` — return `User.Suspended` if the account was suspended after the token was issued.
5. **Revoke** the current token: `token.Revoke()` + `UpdateAsync()`.
6. Issue a new token pair + persist the new refresh token.
7. Return `AuthTokensDto`.

**Refresh token rotation:**
The old token is revoked before the new one is issued. If a token is stolen and the attacker uses it first, the legitimate owner's next refresh attempt finds the token revoked — a detectable compromise. Without rotation, a stolen token is valid for its full remaining lifetime.

**Why accept only the token string and not UserId?**
Accepting a UserId would allow an attacker to claim ownership of any token by passing a different UserId. The token string is the sole credential — the associated UserId is read from the database row, not from the request.

### `RevokeTokenCommand` / `RevokeTokenCommandHandler`

**Input:** `{ Token }`

**Flow:**
1. Look up the token by string value.
2. Return `Token.Invalid` if not found or already inactive.
3. Call `token.Revoke()` — sets `RevokedAt = UtcNow`.
4. Persist the mutation via `UpdateAsync()`.
5. Return `Result.Success()`.

**What is not revoked:** the corresponding access token. Access tokens are stateless JWTs — there is no server-side revocation mechanism without a denylist. The access token continues working until its natural expiry (default 60 min). This is an accepted trade-off of the stateless JWT model.

---

## File Commands and Queries (Level 1 Changes)

All four file operations now include:
- `string? IpAddress` — sourced from `HttpContext.Connection.RemoteIpAddress` in the controller
- `IAuditableRequest` implementation — opts the operation into `AuditLoggingBehaviour`

```csharp
// Example — UploadFileCommand
public string Action       => "File.Upload";
public string ResourceType => "File";
public string? ResourceId  => null;   // GUID not yet known at construction time
```

`ResourceId` is null for `UploadFileCommand` and `ListFilesQuery`. For `DeleteFileCommand` and `DownloadFileQuery`, `ResourceId => FileId.ToString()` — the file GUID is known at construction time.

---

## Service Interfaces

These interfaces are defined here (Application layer) and implemented in `DMS.Infrastructure`. Defining them here preserves the Dependency Inversion Principle — Application depends on the abstraction, not the concrete implementation.

### `IPasswordHasher`

```csharp
string Hash(string password);
bool Verify(string password, string hash);
```

One-way BCrypt hashing. `Verify()` extracts the embedded salt from the stored hash and re-hashes the candidate — necessary because BCrypt generates a random salt per call, so the same password produces different hashes each time.

### `IJwtTokenService`

```csharp
string GenerateAccessToken(Guid userId, string email, Role role);
string GenerateRefreshToken();
```

`GenerateAccessToken` produces a signed JWT embedding the user's identity and role. `GenerateRefreshToken` produces a cryptographically random opaque string (64 bytes from `RandomNumberGenerator`, base64-encoded). The interface is here rather than in Domain because it depends on `JwtSettings` from the Application layer.

### `IEmailService`

```csharp
Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct);
```

Stub in Level 1 — the implementation logs rather than sends. The interface is defined here so command handlers can depend on it; the concrete provider (SendGrid, SMTP) is swapped in `DMS.Infrastructure` without changing any handler.

---

## Settings — `JwtSettings`

```csharp
public sealed record JwtSettings
{
    public string Secret          { get; init; }
    public string Issuer          { get; init; }
    public string Audience        { get; init; }
    public int    ExpiryMinutes   { get; init; }
    public int    RefreshTokenExpiryDays { get; init; }
}
```

**Why in the Application layer (not Infrastructure)?**
`JwtSettings` is the contract — the shape of configuration this layer needs. It belongs here so Application code (handlers, `IJwtTokenService`) can reference it without taking a dependency on Infrastructure. `DMS.Infrastructure` reads the raw config and binds it to this record via `services.Configure<JwtSettings>(...)`.

**Why `init` setters?**
The Options pattern constructs the record via property-based binding (not a constructor call). `init` allows the binder to set properties while still making the record effectively immutable after construction.

---

## DTOs — Data Transfer Objects

| DTO | Used by | Contents |
|---|---|---|
| `FileMetadataDto` | UploadFile, ListFiles | `Id, Filename, FileSize, MimeType, UploadedAt` |
| `FileDownloadResult` | DownloadFile | `Stream Content, string Filename, string MimeType` |
| `AuthTokensDto` | Register, Login, RefreshToken | `AccessToken, RefreshToken, ExpiresInSeconds` |
| `UserDto` | Register | `Id, Email, Role` |

**Why no `PasswordHash` on `UserDto`?**
Structural enforcement — the DTO is the boundary between the application and the API. The password hash is never a field on `UserDto`, so it is physically impossible for a handler to accidentally include it in a response.

**Why `ExpiresInSeconds` on `AuthTokensDto`?**
The client needs to know when to proactively refresh the access token before it expires. Providing the expiry duration (in seconds) lets the client schedule a refresh without parsing the JWT or maintaining a clock.

---

## `DependencyInjection.cs`

```csharp
services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

services.AddScoped(
    typeof(IPipelineBehavior<,>),
    typeof(AuditLoggingBehaviour<,>));
```

**`RegisterServicesFromAssembly`** auto-discovers all `IRequestHandler<,>` implementations. Adding a new handler requires no changes here.

**Open-generic `IPipelineBehavior<,>` registration** — `typeof(IPipelineBehavior<,>)` and `typeof(AuditLoggingBehaviour<,>)` are open generic types (no type arguments). The DI container closes them at resolution time for each specific request type. This single registration covers every `IAuditableRequest` handler automatically.

**Behaviour order = registration order.** If multiple pipeline behaviours are registered, they execute in the order they are added. `AuditLoggingBehaviour` is the only behaviour in Level 1; future behaviours (e.g., validation) would be added before it so validation runs first.

---

## Handler Visibility — `internal sealed`

All handlers are `internal sealed`:

- **`internal`** — an implementation detail of this assembly. No external code should reference `LoginCommandHandler` directly. The contract is the command record, which is `public`.
- **`sealed`** — handlers are not designed for inheritance. Prevents accidental subclassing.

MediatR resolves handlers via the DI container using reflection — `internal` visibility is not an obstacle.

---

## CancellationToken Propagation

Every handler method accepts `CancellationToken ct` and passes it through to every async call (repository, storage, token service). If a client disconnects mid-request, ASP.NET Core cancels the token. Without propagation the handler would continue all I/O operations for a response no one will receive — wasting database connections and CPU under load.
