# Level 3 — Sharing + Permissions

> **Problem:** Users want to share specific files or folders with colleagues without giving full account access. There is currently no way to grant another user read or write access to a resource you own.

Full requirements: [`dms_requirements.txt`](dms_requirements.txt)

---

## Functional Requirements

### Direct Sharing
- Share a file or folder with another registered user, assigning a role: `Viewer`, `Commenter`, or `Editor`
- Revoke a direct share at any time (by the owner or an admin)
- List all shares on a resource (owner only)
- Email notification sent to the invitee on share creation

### Public Links
- Generate a public shareable link (opaque token) for a file or folder
- Optional expiry date on a public link
- Optional password protection on a public link
- Revoke a public link at any time
- Anyone (unauthenticated) with the token can access the resource at the link's granted role level

### Permission Inheritance
- A share on a folder implicitly grants the same role on all descendant folders and files
- A child resource can have its own explicit share that overrides the parent's grant (child wins)
- Effective permission = most specific (nearest-ancestor) explicit grant

### Share Audit Trail
- Every share creation and revocation is recorded: who, what resource, granted-to, role, timestamp
- Stored in the existing `AuditLog` table via `AuditLoggingBehaviour`

---

## Non-Functional Requirements

| Constraint | Value |
|---|---|
| Permission check latency | p99 < 20 ms (Redis-cached resolved permissions) |
| Public link token | Cryptographically random, 32 bytes (64-char hex string) |
| Max shares per resource | 500 principals |
| Redis cache TTL | 60 seconds for resolved permission entries |
| Public link password | Bcrypt-hashed (same hasher as user passwords) |

---

## Key Technologies
ACL table (`Shares`), `PublicLinks` table, Redis `IDistributedCache` for permission cache, `IPermissionService` for resolution + caching, `IEmailService` (existing) for share invitations

---

## DMS.Domain

### New Entities

#### `Share`
| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Application-generated |
| `ResourceId` | `Guid` | ID of the shared file or folder |
| `ResourceType` | `ShareResourceType` | `File` or `Folder` |
| `GrantedToUserId` | `Guid` | The user receiving access |
| `GrantedByUserId` | `Guid` | The owner who created the share |
| `Role` | `ShareRole` | `Viewer`, `Commenter`, or `Editor` |
| `CreatedAt` | `DateTimeOffset` | UTC timestamp |

Domain methods:
- `Create(resourceId, resourceType, grantedToUserId, grantedByUserId, role)` — static factory
- `IsGrantedTo(userId)` — checks `GrantedToUserId == userId`

#### `PublicLink`
| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Application-generated |
| `ResourceId` | `Guid` | ID of the shared file or folder |
| `ResourceType` | `ShareResourceType` | `File` or `Folder` |
| `Token` | `string` | 64-char hex string (32 random bytes); unique index |
| `Role` | `ShareRole` | Role granted to anyone with this link |
| `CreatedByUserId` | `Guid` | Owner who created the link |
| `ExpiresAt` | `DateTimeOffset?` | Null = never expires |
| `PasswordHash` | `string?` | Null = no password required |
| `CreatedAt` | `DateTimeOffset` | UTC timestamp |

Domain methods:
- `Create(resourceId, resourceType, token, role, createdByUserId, expiresAt?, passwordHash?)` — static factory
- `IsExpired()` → `ExpiresAt.HasValue && ExpiresAt.Value < UtcNow`
- `RequiresPassword()` → `PasswordHash != null`

### New Enums

#### `ShareRole`
```csharp
public enum ShareRole { Viewer = 0, Commenter = 1, Editor = 2 }
```
Ordered by ascending access level so `role >= ShareRole.Viewer` reads naturally.

#### `ShareResourceType`
```csharp
public enum ShareResourceType { File, Folder }
```

### New Repository Interfaces

#### `IShareRepository`
```
GetByIdAsync(id)                              → Share?
GetByResourceAsync(resourceId, resourceType)  → IReadOnlyList<Share>
GetForUserAsync(userId)                       → IReadOnlyList<Share>
GetDirectShareAsync(resourceId, grantedToId)  → Share?
AddAsync(share)
DeleteAsync(share)
```

#### `IPublicLinkRepository`
```
GetByIdAsync(id)                 → PublicLink?
GetByTokenAsync(token)           → PublicLink?
GetByResourceAsync(resourceId)   → IReadOnlyList<PublicLink>
AddAsync(link)
DeleteAsync(link)
```

### New Service Interface

#### `IPermissionService`
Defined in Domain (so Application handlers can depend on it).
```
CanReadAsync(userId, resourceId, resourceType, ct)    → Task<bool>
CanWriteAsync(userId, resourceId, resourceType, ct)   → Task<bool>
CanCommentAsync(userId, resourceId, resourceType, ct) → Task<bool>
```
Resolution logic (owned by the Infrastructure implementation):
1. If the user owns the resource → allow all
2. Check for an explicit `Share` row on this resource for this user
3. Walk up the folder ancestry for inherited shares (nearest ancestor wins)
4. Cache the resolved result in Redis with a 60 s TTL

### New Domain Errors

#### `DomainErrors.Share`
| Error | Code | HTTP |
|---|---|---|
| `NotFound` | `Share.NotFound` | 404 |
| `AlreadyExists` | `Share.AlreadyExists` | 409 |
| `MaxSharesExceeded` | `Share.MaxSharesExceeded` | 422 |
| `CannotShareWithSelf` | `Share.CannotShareWithSelf` | 400 |

#### `DomainErrors.PublicLink`
| Error | Code | HTTP |
|---|---|---|
| `NotFound` | `PublicLink.NotFound` | 404 |
| `Expired` | `PublicLink.Expired` | 410 |
| `InvalidPassword` | `PublicLink.InvalidPassword` | 401 |

---

## DMS.Application

### New DTOs

| DTO | Contents | Used By |
|---|---|---|
| `ShareDto` | `Id, ResourceId, ResourceType, GrantedToUserId, GrantedToEmail, Role, CreatedAt` | ListShares |
| `PublicLinkDto` | `Id, ResourceId, ResourceType, Token, Role, ExpiresAt?, HasPassword, CreatedAt` | CreatePublicLink, ListPublicLinks |
| `SharedResourceDto` | `Id, Name, ResourceType, Role, SharedByUserId, SharedAt` | ListSharedWithMe |

### New Share Commands

| Command | Input | Returns | Notes |
|---|---|---|---|
| `CreateShareCommand` | `ResourceId, ResourceType, GrantedToEmail, Role, GrantedByUserId, IpAddress?` | `Result<ShareDto>` | Resolves email → UserId; sends email notification; implements `IAuditableRequest` |
| `RevokeShareCommand` | `ShareId, RequestingUserId` | `Result` | Owner or admin only; implements `IAuditableRequest` |

### New Public Link Commands

| Command | Input | Returns | Notes |
|---|---|---|---|
| `CreatePublicLinkCommand` | `ResourceId, ResourceType, Role, CreatedByUserId, ExpiresAt?, Password?` | `Result<PublicLinkDto>` | Generates secure 32-byte token; bcrypt-hashes password if provided |
| `RevokePublicLinkCommand` | `LinkId, RequestingUserId` | `Result` | Owner only |

### New Share Queries

| Query | Input | Returns |
|---|---|---|
| `ListSharesQuery` | `ResourceId, ResourceType, RequestingUserId` | `Result<IReadOnlyList<ShareDto>>` |
| `ListSharedWithMeQuery` | `UserId` | `Result<IReadOnlyList<SharedResourceDto>>` |
| `ListPublicLinksQuery` | `ResourceId, ResourceType, RequestingUserId` | `Result<IReadOnlyList<PublicLinkDto>>` |
| `ResolvePublicLinkQuery` | `Token, Password?` | `Result<PublicLinkDto>` |

### Updated File and Folder Handlers

Every existing file/folder command handler and query handler that checks `BelongsTo(userId)` gains a **secondary permission check** via `IPermissionService`:

```
Before: if (resource is null || !resource.BelongsTo(userId)) → NotFound/Forbidden
After:  if (resource is null)                                 → NotFound
        if (!resource.BelongsTo(userId) &&
            !await permissionService.CanReadAsync(...))       → Forbidden
```

Write operations (`UploadFile`, `DeleteFile`, `RenameFolder`, etc.) check `CanWriteAsync`. Read operations (`DownloadFile`, `GetFolderContents`, etc.) check `CanReadAsync`.

### `DependencyInjection.cs` — No changes needed
MediatR auto-discovers new handlers. `IPermissionService` and repositories are registered in Infrastructure.

---

## DMS.Infrastructure

### New EF Configurations

#### `ShareConfiguration`
- `ToTable("Shares")`
- `ValueGeneratedNever()` on `Id`
- `HasConversion<int>()` on `Role` and `ResourceType` enums
- Unique index on `(ResourceId, GrantedToUserId)` → prevents duplicate grants
- Index on `(GrantedToUserId)` → for `ListSharedWithMe` query
- Index on `(ResourceId)` → for `ListShares` query

#### `PublicLinkConfiguration`
- `ToTable("PublicLinks")`
- `ValueGeneratedNever()` on `Id`
- `HasMaxLength(64)` on `Token`; unique index → `UX_PublicLinks_Token`
- `HasMaxLength(255)` on `PasswordHash` (nullable)
- Index on `(ResourceId)` → for `ListPublicLinks` query

### New Repository Implementations

#### `ShareRepository`
Standard EF Core LINQ. `GetByResourceAsync` and `GetForUserAsync` use `.ToListAsync()`.

#### `PublicLinkRepository`
`GetByTokenAsync` uses `.FirstOrDefaultAsync(l => l.Token == token)` backed by the unique index.

### New Service Implementation

#### `PermissionService`
Implements `IPermissionService`. Injected with `AppDbContext`, `IDistributedCache`, `IFolderRepository`.

Resolution algorithm for `CanReadAsync(userId, resourceId, resourceType)`:
1. Build a Redis key: `perm:{userId}:{resourceType}:{resourceId}`
2. Cache hit → deserialize and return
3. Cache miss:
   a. If `resourceType == File`: load `FileMetadata`, check `UserId == userId` → owner
   b. If `resourceType == Folder`: load `Folder`, check `OwnerId == userId` → owner
   c. Query `Shares` for an explicit share on this resource for this user
   d. If no direct share: walk folder ancestry (file's `FolderId`, then each folder's `ParentFolderId`) querying `Shares` at each level — first hit is the effective role
   e. Write resolved role (or `None`) to Redis with 60 s TTL
4. Return `resolvedRole >= requiredRole`

### Redis Registration
```csharp
services.AddStackExchangeRedisCache(opts =>
    opts.Configuration = configuration.GetConnectionString("Redis"));
```
Registered in `DependencyInjection.cs`. For local dev without Redis, falls back gracefully to a no-op cache (resolved on every request) by catching `RedisConnectionException`.

### New EF Migration

#### `AddSharingAndPublicLinks`
Creates:
- `Shares` table with unique constraint on `(ResourceId, GrantedToUserId)`
- `PublicLinks` table with unique index on `Token`

---

## DMS.Api

### New Controller — `SharesController` (`/api/shares`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/shares` | JWT | Create a direct share |
| `DELETE` | `/api/shares/{id:guid}` | JWT | Revoke a direct share |
| `GET` | `/api/shares?resourceId=&resourceType=` | JWT | List all shares for a resource (owner only) |
| `GET` | `/api/shares/me` | JWT | List all resources shared with the caller |

### New Controller — `PublicLinksController` (`/api/public`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/public` | JWT | Create a public link |
| `DELETE` | `/api/public/{id:guid}` | JWT | Revoke a public link |
| `GET` | `/api/public?resourceId=&resourceType=` | JWT | List public links for a resource (owner only) |
| `GET` | `/api/public/{token}` | **None** | Resolve a public link (unauthenticated) |
| `POST` | `/api/public/{token}/unlock` | **None** | Submit password to unlock a password-protected link |

### Updated `FilesController` and `FoldersController`
- `DownloadFile`, `GetFolderContents`, `GetRoot` — use `IPermissionService.CanReadAsync`
- `UploadFile` (into a folder), `DeleteFile`, `RenameFolder`, `MoveFolder`, `DeleteFolder` — use `IPermissionService.CanWriteAsync`
- Non-owners receive `403 Forbidden` rather than `404 Not Found` when they have no share

### `ResultExtensions` — New Error Mappings

| Error | HTTP Status |
|---|---|
| `Share.NotFound` | 404 |
| `Share.AlreadyExists` | 409 |
| `Share.MaxSharesExceeded` | 422 |
| `Share.CannotShareWithSelf` | 400 |
| `PublicLink.NotFound` | 404 |
| `PublicLink.Expired` | 410 |
| `PublicLink.InvalidPassword` | 401 |

---

## Implementation Order

```
1. DMS.Domain
   └── ShareRole enum + ShareResourceType enum
   └── Share entity + IShareRepository
   └── PublicLink entity + IPublicLinkRepository
   └── IPermissionService interface
   └── DomainErrors.Share + DomainErrors.PublicLink

2. DMS.Application
   └── DTOs (ShareDto, PublicLinkDto, SharedResourceDto)
   └── Share commands: CreateShare, RevokeShare
   └── Public link commands: CreatePublicLink, RevokePublicLink
   └── Share queries: ListShares, ListSharedWithMe, ListPublicLinks, ResolvePublicLink
   └── Update existing file/folder handlers to call IPermissionService

3. DMS.Infrastructure
   └── ShareConfiguration + PublicLinkConfiguration
   └── ShareRepository + PublicLinkRepository
   └── PermissionService (resolution + Redis caching)
   └── Redis IDistributedCache registration in DependencyInjection
   └── DependencyInjection: register new repos + PermissionService
   └── Migration: AddSharingAndPublicLinks

4. DMS.Api
   └── ResultExtensions updated (Share + PublicLink error codes)
   └── SharesController
   └── PublicLinksController (includes unauthenticated token endpoint)
   └── FilesController + FoldersController updated (IPermissionService guards)
```

---

## Key Design Decisions

### Permission resolution order
Child-level explicit share always wins over parent-inherited share. If a user has `Editor` on a folder and `Viewer` on a specific subfolder, their effective role on the subfolder (and its children) is `Viewer`. This is stored per-resource in Redis — invalidating the cache for a resource is done by deleting its key on any share modification.

### Redis fallback for local dev
`PermissionService` catches `RedisConnectionException` and falls back to computing permissions from the DB on every request. This keeps the project runnable without Docker during development at the cost of latency.

### Commenter role scope
`Commenter` grants read access identical to `Viewer` until Level-7 (real-time collaboration) adds inline comments. The role is enforced in the ACL table now so that when comment endpoints are added, the permission model is already in place with no migration needed.

### Public link authentication flow
- No-password links: `GET /api/public/{token}` returns the resource metadata or a redirect URL directly
- Password-protected links: `GET /api/public/{token}` returns `401 PublicLink.InvalidPassword`; client `POST /api/public/{token}/unlock` with `{ "password": "..." }` returns a short-lived signed access token (JWT, 15 min, audience = `public-link:{token}`) that can be used to download the resource

### Share count enforcement
`CreateShareCommandHandler` queries `COUNT(*) FROM Shares WHERE ResourceId = @id` before inserting. If count >= 500, returns `Share.MaxSharesExceeded` (422).

### Cache invalidation
On `CreateShare`, `RevokeShare`, `CreatePublicLink`, `RevokePublicLink`: the handler (or a MediatR notification) deletes all Redis keys matching `perm:*:{resourceType}:{resourceId}`. Pattern-delete is done via `IDistributedCache.RemoveAsync` on specific known keys (one per affected user) rather than a SCAN — avoids requiring Redis `KEYS` permission.
