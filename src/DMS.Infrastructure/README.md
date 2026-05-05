# DMS.Infrastructure

The infrastructure layer is where abstractions meet reality. It provides the concrete implementations of every interface defined in `DMS.Domain` — wiring SQL Server, EF Core, BCrypt, JWT generation, and the local file system into the contracts the domain declared it needs. No other layer knows these implementations exist; they are resolved purely through dependency injection.

---

## Position in the Architecture

```
┌─────────────┐
│   DMS.Api   │
└──────┬──────┘
       │
       ▼
┌──────────────────┐
│ DMS.Application  │  calls IUserRepository, IRefreshTokenRepository,
│                  │        IAuditLogRepository, IFileMetadataRepository,
│                  │        IFileStorageService, IPasswordHasher, IJwtTokenService
└──────────────────┘
         ▲  (interfaces defined in DMS.Domain / DMS.Application)
         │
┌────────┴─────────────┐   ◄── this layer
│  DMS.Infrastructure  │
│                      │
│  UserRepository      │  implements IUserRepository
│  RefreshTokenRepo    │  implements IRefreshTokenRepository
│  AuditLogRepository  │  implements IAuditLogRepository
│  FileMetadataRepo    │  implements IFileMetadataRepository
│  LocalFileStorage    │  implements IFileStorageService
│  PasswordHasher      │  implements IPasswordHasher
│  JwtTokenService     │  implements IJwtTokenService
│  EmailService        │  implements IEmailService (stub)
│  AppDbContext        │  EF Core unit of work
└──────────────────────┘
         │
         ▼
  SQL Server + Local Disk
```

This layer depends on `DMS.Domain` and `DMS.Application` (to implement their interfaces) and on external frameworks (EF Core, BCrypt.Net, JWT). It is the only layer that imports infrastructure-specific NuGet packages.

---

## Level 1 Changes from Level 0

| Area | Level 0 | Level 1 |
|---|---|---|
| Auth services | None | `PasswordHasher`, `JwtTokenService`, `EmailService` |
| Repositories | `FileMetadataRepository` | + `UserRepository`, `RefreshTokenRepository`, `AuditLogRepository` |
| EF configurations | `FileMetadataConfiguration` | + `UserConfiguration`, `RefreshTokenConfiguration`, `AuditLogConfiguration` |
| DbContext DbSets | `FileMetadata` | + `Users`, `RefreshTokens`, `AuditLogs` |
| DI registrations | 2 Scoped | + 2 Singleton, 1 Scoped, 3 Scoped repositories; `JwtSettings` binding |
| Migrations | `InitialCreate` | + `AddAuthAndAudit` |

---

## Project Structure

```
DMS.Infrastructure/
├── Auth/
│   ├── EmailService.cs                              # IEmailService stub (logs instead of sends)
│   ├── JwtTokenService.cs                           # IJwtTokenService — JWT + refresh token generation
│   └── PasswordHasher.cs                            # IPasswordHasher — BCrypt hash and verify
├── Persistence/
│   ├── AppDbContext.cs                              # EF Core DbContext — unit of work
│   ├── Configurations/
│   │   ├── AuditLogConfiguration.cs                # Fluent API: AuditLogs table + indexes
│   │   ├── FileMetadataConfiguration.cs            # Fluent API: FileMetadata table + index
│   │   ├── RefreshTokenConfiguration.cs            # Fluent API: RefreshTokens table + indexes
│   │   └── UserConfiguration.cs                    # Fluent API: Users table + unique email index
│   └── Repositories/
│       ├── AuditLogRepository.cs                   # IAuditLogRepository implementation
│       ├── FileMetadataRepository.cs               # IFileMetadataRepository implementation
│       ├── RefreshTokenRepository.cs               # IRefreshTokenRepository implementation
│       └── UserRepository.cs                       # IUserRepository implementation
├── Storage/
│   └── LocalFileStorageService.cs                  # IFileStorageService implementation (local disk)
├── Migrations/
│   ├── 20260421141810_InitialCreate.cs             # FileMetadata table
│   ├── 20260423132247_AddAuthAndAudit.cs           # Users, RefreshTokens, AuditLogs tables
│   └── AppDbContextModelSnapshot.cs                # EF Core model snapshot (do not edit manually)
└── DependencyInjection.cs                          # Registers all infrastructure services
```

---

## `DependencyInjection.cs` — Wiring the Layer

This is the only place in the entire codebase where infrastructure types are named explicitly. All other layers reference only the domain/application interfaces.

```csharp
// JwtSettings bound from "Jwt" section of appsettings.json
services.Configure<JwtSettings>(opts => configuration.Bind("Jwt", opts));

// EF Core
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

// Repositories — Scoped (must match AppDbContext lifetime)
services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();
services.AddScoped<IUserRepository,         UserRepository>();
services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
services.AddScoped<IAuditLogRepository,     AuditLogRepository>();

// Storage — Scoped
services.AddScoped<IFileStorageService, LocalFileStorageService>();

// Auth services — Singleton (stateless) and Scoped (email)
services.AddSingleton<IPasswordHasher,   PasswordHasher>();
services.AddSingleton<IJwtTokenService,  JwtTokenService>();
services.AddScoped<IEmailService,        EmailService>();
```

### Lifetime decisions

**Scoped for all repositories:**
All repositories must match `AppDbContext`'s Scoped lifetime. A Singleton repository holding a Scoped `DbContext` would cause an `ObjectDisposedException` on the second request — the first request disposes the `DbContext`; the Singleton still holds the dead reference.

**Singleton for `PasswordHasher` and `JwtTokenService`:**
Both are stateless — they hold no mutable per-request state. `PasswordHasher` is a thin wrapper around BCrypt. `JwtTokenService` reads `IOptions<JwtSettings>` (also Singleton) and produces deterministic output from its inputs. Singleton avoids re-allocating these objects on every request.

**Scoped for `EmailService`:**
Currently a stub, but a real email provider would likely need an `IHttpClientFactory`-backed HTTP client, which should be Scoped or Transient. Registering `EmailService` as Scoped makes the lifetime upgrade to a real provider seamless.

---

## Auth Services

### `PasswordHasher`

```csharp
internal sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
```

**Why BCrypt over SHA-256 or MD5?**
BCrypt is intentionally slow (controlled by `workFactor`) and automatically generates and embeds a random salt per hash. Cryptographic hash functions (SHA-256, MD5) are designed to be fast — easy to brute-force with GPU farms. BCrypt at cost 12 takes ~250ms per attempt, making offline dictionary attacks impractical.

**`workFactor: 12`** — performs 2^12 rounds of key stretching. OWASP recommends a minimum of 10; 12 is the common production value balancing security and login latency.

**Why `Verify()` instead of hashing again and comparing?**
BCrypt embeds the random salt inside the hash string (the `$2a$12$...` prefix). `Verify()` extracts that salt, re-hashes the candidate password with it, and compares. Re-hashing with a fresh random salt would always produce a different string and never match.

---

### `JwtTokenService`

```csharp
internal sealed class JwtTokenService(IOptions<JwtSettings> jwtOptions) : IJwtTokenService
```

**`GenerateAccessToken`** — produces a signed JWT with four claims:

| Claim | Value | Purpose |
|---|---|---|
| `sub` | `userId.ToString()` | Identity — ASP.NET Core maps this to `ClaimTypes.NameIdentifier` |
| `email` | user's email | Convenience — clients can decode the email without a profile endpoint |
| `ClaimTypes.Role` | `role.ToString()` | Authorization — `[Authorize(Roles = "Admin")]` reads this |
| `jti` | `Guid.NewGuid()` | Unique token ID — enables future denylist-based revocation |

Uses HMAC-SHA256 (symmetric signing) — sufficient when the same service both issues and verifies tokens. Asymmetric (RSA) is needed only when tokens are verified by a third party.

**`GenerateRefreshToken`** — produces a cryptographically random opaque string:
```csharp
var bytes = RandomNumberGenerator.GetBytes(64);
return Convert.ToBase64String(bytes);
```
`RandomNumberGenerator` is a CSPRNG — full entropy over all bits. `Guid.NewGuid()` is a pseudo-random algorithm unsuitable for security tokens. 64 bytes base64-encoded produces an ~88-character string, comfortably within the `MaxLength(512)` column constraint.

---

### `EmailService`

```csharp
internal sealed class EmailService(ILogger<EmailService> logger) : IEmailService
```

A development stub — logs the email content instead of sending it. The `[EMAIL STUB]` prefix makes it immediately visible in log output during development.

Named log placeholders (`{Email}`, `{ResetLink}`) are used instead of string interpolation so Serilog captures them as structured, queryable properties rather than a flat string.

**TODO:** Replace with a real provider (SendGrid, AWS SES, SMTP) before deploying to production.

---

## `AppDbContext` — Unit of Work

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FileMetadata> FileMetadata  => Set<FileMetadata>();
    public DbSet<User>         Users         => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog>     AuditLogs     => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

`AppDbContext` is the EF Core Unit of Work — it tracks all entity changes within a request and flushes them to SQL Server via `SaveChangesAsync`. One instance per HTTP request (Scoped lifetime via `AddDbContext`).

**`DbSet<T>` as computed properties (`=> Set<T>()`):**
`Set<T>()` is EF Core's recommended pattern for DbSet exposure in modern applications. An auto-property (`{ get; set; }`) requires manual initialisation; the computed form is always backed by the live context instance and cannot be accidentally null.

**`ApplyConfigurationsFromAssembly`:**
Any class implementing `IEntityTypeConfiguration<T>` in `DMS.Infrastructure` is discovered and applied automatically. Adding a new entity requires only a new configuration file — `OnModelCreating` never needs editing.

---

## EF Core Configurations

All configurations implement `IEntityTypeConfiguration<T>` and use the Fluent API exclusively. Data Annotations are intentionally avoided — they would require `DMS.Domain` entities to reference `Microsoft.EntityFrameworkCore`, coupling the domain to infrastructure.

### `UserConfiguration`

| Setting | Value | Reason |
|---|---|---|
| `ValueGeneratedNever()` on Id | GUID from `User.Create()` | Without this, EF omits the Id from INSERT |
| `HasMaxLength(255)` on Email | RFC 5321 max email = 254 chars | Bounded column; indexable |
| `HasMaxLength(255)` on PasswordHash | BCrypt output is 60 chars | Room for format changes |
| `HasConversion<string>()` on Role | Stores `"User"` / `"Admin"` | Ordinal integers break if enum order changes |
| `HasIndex(u => u.Email).IsUnique()` | `IX_Users_Email` | Login lookup + duplicate registration prevention |

**Why `HasConversion<string>()` for the Role enum?**
Integer storage is brittle: inserting a new role before `Admin` in the enum declaration silently shifts all stored ordinals. String storage is self-documenting and survives enum reordering. The column uses `HasMaxLength(50)` — no role name approaches that length.

---

### `RefreshTokenConfiguration`

| Setting | Value | Reason |
|---|---|---|
| `ValueGeneratedNever()` on Id | GUID from `RefreshToken.Create()` | Same as above |
| `HasMaxLength(512)` on Token | Base64(64 bytes) = 88 chars | Generous headroom for strategy changes |
| `RevokedAt` — no `IsRequired()` | Nullable column | `null` = not yet revoked; value = when revoked |
| `HasIndex(rt => rt.Token).IsUnique()` | `IX_RefreshTokens_Token` | O(log n) lookup on every Refresh/Revoke call |
| `HasIndex(rt => rt.UserId)` | `IX_RefreshTokens_UserId` | Future "logout all devices" query support |

`RevokedAt` is intentionally absent from `IsRequired()`. The nullable column is the persistence form of the domain's `DateTimeOffset? RevokedAt` property — null means the token has never been revoked.

---

### `AuditLogConfiguration`

| Setting | Value | Reason |
|---|---|---|
| `ValueGeneratedNever()` on Id | GUID from `AuditLog.Create()` | Same as above |
| `HasMaxLength(45)` on IpAddress | IPv4-mapped IPv6 is max 45 chars | Covers all address formats |
| `ResourceId` — no `IsRequired()` | Nullable column | Null for collection ops and Upload |
| `HasIndex(a => a.UserId)` | `IX_AuditLogs_UserId` | "Show all actions by user X" queries |
| `HasIndex(a => a.OccurredAt)` | `IX_AuditLogs_OccurredAt` | Time-range queries ("last hour of activity") |

No FK constraint from `UserId` to the `Users` table — audit records must survive user deletion (compliance, forensics). `UserId` is a plain string column.

---

### `FileMetadataConfiguration`

| Setting | Value | Reason |
|---|---|---|
| `ValueGeneratedNever()` on Id | GUID from `FileMetadata.Create()` | Same as above |
| `HasMaxLength(255)` on Filename | Common OS filename limit | Bounded column |
| `HasMaxLength(100)` on MimeType | All IANA types fit within 100 | Bounded column |
| `HasMaxLength(500)` on StoragePath | Local paths + future cloud keys | Bounded column |
| `HasIndex(f => f.UserId)` | `IX_FileMetadata_UserId` | O(log n + k) list queries vs O(n) table scan |

No FK constraint from `UserId` to `Users` — consistent with the audit log pattern; file records outlive the user's active status (soft deletion model).

---

## Repositories

All repositories are `internal sealed` — implementation details of this assembly. Application code only ever sees the domain interface type.

### `UserRepository`

```csharp
internal sealed class UserRepository(AppDbContext dbContext) : IUserRepository
```

- **`GetByIdAsync`** — uses `FindAsync([id], ct)`: checks the EF identity map first, falls back to a SQL query on miss. More efficient than `FirstOrDefaultAsync` for PK lookups.
- **`GetByEmailAsync`** — uses `FirstOrDefaultAsync` (not `FindAsync` — email is not the PK). Normalises the input with `.ToLowerInvariant()` as defence-in-depth against mixed-case logins.
- **`UpdateAsync`** — calls `dbContext.Users.Update(user)` to mark all properties as Modified, then `SaveChangesAsync`. Required to persist mutations from `Suspend()` / `Activate()`.

---

### `RefreshTokenRepository`

```csharp
internal sealed class RefreshTokenRepository(AppDbContext dbContext) : IRefreshTokenRepository
```

- **`GetByTokenAsync`** — `FirstOrDefaultAsync` with a `WHERE Token = @token` predicate. Uses `IX_RefreshTokens_Token` for an efficient lookup. The application never looks up tokens by their GUID primary key.
- **`UpdateAsync`** — persists the `RevokedAt` mutation set by `RefreshToken.Revoke()`.

---

### `AuditLogRepository`

```csharp
internal sealed class AuditLogRepository(AppDbContext dbContext) : IAuditLogRepository
```

Single-method implementation — a direct mirror of the append-only interface. `AddAsync` is the only write operation; no Update or Delete is ever called on the `AuditLogs` DbSet.

The caller (`AuditLoggingBehaviour`) wraps this in a try/catch — audit write failure must not propagate as a 500 error to the client. No try/catch lives in the repository itself; error handling belongs at the caller level.

---

### `FileMetadataRepository`

```csharp
internal sealed class FileMetadataRepository(AppDbContext dbContext) : IFileMetadataRepository
```

- **`GetByIdAsync`** — `FindAsync` (identity map → database).
- **`GetByUserIdAsync`** — `WHERE UserId = @userId ORDER BY UploadedAt DESC`, fully materialised with `ToListAsync`. Sorting is database-side; `IReadOnlyList<T>` return type prevents deferred execution after the DbContext scope ends.
- **`DeleteAsync`** — `Remove(file)` (synchronous, change tracker only) then `SaveChangesAsync`. The entity is already loaded by the time it reaches the repository — no second lookup needed.

---

## `LocalFileStorageService` — File System Implementation

```csharp
internal sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
```

The Level 1 storage backend — files on the same machine as the API. Swapping this for a cloud provider requires only registering a different `IFileStorageService` implementation in `DependencyInjection.cs`.

### Storage Layout

```
uploads/
└── {userId}/
    └── {fileId}       ← GUID, no extension
```

Files are named by GUID (not original filename) to prevent path traversal attacks and filename collisions. The original filename is preserved in the `FileMetadata` database row for display only.

### Key Implementation Decisions

| Decision | Reason |
|---|---|
| `Directory.CreateDirectory` (no exist check) | Idempotent — safe to call unconditionally |
| `FileShare.None` during write | No concurrent partial reads while bytes are being written |
| `await using var fileStream` | Disposes and flushes even if `CopyToAsync` throws |
| `FileShare.Read` during read | Multiple concurrent downloads of the same file |
| `Task.FromResult(stream)` in `ReadAsync` | File open is synchronous; avoids a state machine allocation |
| `File.Exists` guard + `FileNotFoundException` | Missing physical file = integrity violation, not a Result failure |
| `File.Exists` guard in `DeleteAsync` | Idempotent retry safety (crash between file delete and metadata delete) |
| `Task.CompletedTask` in `DeleteAsync` | Synchronous operation; returns cached completed Task — no allocation |

---

## Migrations

EF Core migrations are the version-controlled history of the database schema. Applied via:

```bash
dotnet ef database update --project src/DMS.Infrastructure --startup-project src/DMS.Api
```

### `20260421141810_InitialCreate`

Creates the `FileMetadata` table with all columns and `IX_FileMetadata_UserId`. The `Down` method drops the table — clean rollback to an empty database.

### `20260423132247_AddAuthAndAudit`

Adds the three Level 1 tables:
- **`Users`** — with `IX_Users_Email` unique index
- **`RefreshTokens`** — with `IX_RefreshTokens_Token` unique index and `IX_RefreshTokens_UserId`
- **`AuditLogs`** — with `IX_AuditLogs_UserId` and `IX_AuditLogs_OccurredAt`

### `AppDbContextModelSnapshot`

EF Core maintains a snapshot of the current full model. When `dotnet ef migrations add` runs, EF diffs the live domain model against this snapshot to generate only the incremental changes. Never edit this file manually — it is owned entirely by the EF Core tooling.

---

## `internal sealed` — Visibility of Implementations

All infrastructure types are `internal sealed`:

- **`internal`** — implementation details of `DMS.Infrastructure`. No other assembly can reference them directly. `DMS.Application` and `DMS.Api` receive them as the interface type through the DI container.
- **`sealed`** — not designed for inheritance. Single, specific responsibility. Signals a leaf in the type hierarchy.
