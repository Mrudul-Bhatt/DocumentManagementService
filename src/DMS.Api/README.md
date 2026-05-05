# DMS.Api

The entry point of the Document Management Service. This layer owns the HTTP surface — routing, request binding, response shaping, authentication enforcement, and cross-cutting concerns like exception handling and Swagger documentation. It contains no business logic; its sole responsibility is to translate HTTP into application commands/queries and map results back to HTTP responses.

---

## Position in the Architecture

```
HTTP Request
     │
     ▼
┌─────────────┐
│   DMS.Api   │  ◄── this layer
└──────┬──────┘
       │  IMediator (sends Commands / Queries)
       ▼
┌──────────────────┐
│ DMS.Application  │
└──────────────────┘
```

The Api layer depends on `DMS.Application` (for commands, queries, and the `Result` type) and `DMS.Domain` (for `DomainErrors`). It never references `DMS.Infrastructure` directly — that dependency is wired by the DI container at startup.

---

## Level 1 Changes from Level 0

| Area | Level 0 | Level 1 |
|---|---|---|
| Identity | `X-User-Id` trusted header | JWT Bearer token — cryptographically verified |
| Auth enforcement | None | `[Authorize]` on `FilesController` (class level) |
| User extraction | `Request.Headers["X-User-Id"]` | `User.GetUserId()` reads JWT `NameIdentifier` claim |
| Swagger auth | `UserIdHeaderOperationFilter` injects header | Bearer `AddSecurityDefinition` + global `AddSecurityRequirement` |
| New controller | — | `AuthController` (Register, Login, Refresh, Revoke) |
| New extension | — | `ClaimsPrincipalExtensions.GetUserId()` |
| Rate limiting | None | Sliding window on `POST /auth/login` (5 req/min) |
| Middleware pipeline | Exception → Swagger → HTTPS → Controllers | Exception → Swagger → HTTPS → **RateLimiter → Authentication → Authorization** → Controllers |
| Audit logging | None | IP captured and forwarded to every command/query |
| Removed | — | `Swagger/UserIdHeaderOperationFilter.cs` (replaced by Bearer scheme) |

---

## Project Structure

```
DMS.Api/
├── Controllers/
│   ├── AuthController.cs           # Register, Login, Refresh, Revoke endpoints
│   └── FilesController.cs          # Upload, List, Download, Delete endpoints
├── Extensions/
│   ├── ClaimsPrincipalExtensions.cs # GetUserId() — reads UserId from JWT claim
│   └── ResultExtensions.cs         # Maps Result<T> failures to RFC 7807 ProblemDetails
├── Middleware/
│   └── GlobalExceptionMiddleware.cs # Last-resort unhandled exception handler
└── Program.cs                      # Service wiring, middleware pipeline configuration
```

---

## Program.cs — Startup & Middleware Pipeline

`Program.cs` uses the ASP.NET Core minimal hosting model. It configures services and assembles the middleware pipeline.

### Service Registration

| Registration | Purpose |
|---|---|
| `AddApplication()` | Registers MediatR handlers and `AuditLoggingBehaviour` from `DMS.Application` |
| `AddInfrastructure(config)` | Registers EF Core, repositories, JWT service, password hasher from `DMS.Infrastructure` |
| `AddAuthentication().AddJwtBearer(...)` | Validates JWT Bearer tokens on every request; populates `HttpContext.User` |
| `AddAuthorization()` | Registers the policy engine required for `[Authorize]` to function |
| `AddRateLimiter(...)` | Sliding window policy `"login"`: 5 req/min, no queuing, 429 on excess |
| `AddSwaggerGen` + Bearer security | Single "Authorize" button in Swagger UI for the Bearer token |
| `AddHealthChecks().AddSqlServer(...)` | `/health` endpoint that pings SQL Server |

### JWT Bearer Validation Parameters

```csharp
new TokenValidationParameters
{
    ValidateIssuer           = true,   // rejects tokens from wrong issuers
    ValidateAudience         = true,   // rejects tokens meant for other services
    ValidateLifetime         = true,   // rejects expired tokens
    ValidateIssuerSigningKey = true,   // verifies HMAC-SHA256 signature
    ClockSkew                = TimeSpan.Zero  // no expiry leeway — exact expiry time enforced
}
```

`ClockSkew = TimeSpan.Zero` removes the default 5-minute grace period. With the access token defaulting to 60 minutes, the extra 5 minutes of leeway is unnecessary and reduces security. The client should refresh before expiry rather than relying on server-side leeway.

### Rate Limiter — Sliding Window

```
Policy name  : "login"
PermitLimit  : 5 requests
Window       : 1 minute
Segments     : 6 (10-second granularity)
QueueLimit   : 0 (reject immediately — no queuing under attack)
Rejection    : 429 Too Many Requests
```

**Why sliding window over fixed window?**
A fixed window resets at a hard boundary — an attacker can send 5 requests at 0:59 and 5 more at 1:00 (10 requests in 2 seconds). A sliding window tracks requests over a rolling period, preventing this burst at window boundaries.

**Why QueueLimit = 0?**
Queuing excess requests holds connections open under attack, consuming server threads. Immediate rejection with 429 is the correct response under brute-force conditions.

### Middleware Pipeline Order

```
Request
  │
  ├── 1. GlobalExceptionMiddleware   (catch-all — outermost wrapper)
  ├── 2. Swagger / SwaggerUI         (dev only)
  ├── 3. UseHttpsRedirection
  ├── 4. UseRateLimiter              (reject abusive requests before auth cost)
  ├── 5. UseAuthentication           (validate JWT, populate HttpContext.User)
  ├── 6. UseAuthorization            (enforce [Authorize] — must follow Authentication)
  ├── 7. MapControllers
  └── 8. MapHealthChecks("/health")
```

**Why RateLimiter before Authentication?**
Rate limiting should reject abusive traffic before the CPU cost of JWT parsing and cryptographic signature verification. An attacker hammering the login endpoint should be turned away at the rate limiter.

**Why Authentication before Authorization?**
Authorization checks what the caller is *allowed* to do — but only after Authentication establishes *who* they are by populating `HttpContext.User`. Reversing the order means all `[Authorize]` checks fail even for valid tokens.

---

## Controllers

### `AuthController` — `/api/auth`

Handles all authentication operations. No class-level `[Authorize]` — Register, Login, and Refresh must be publicly accessible because they are how callers obtain credentials in the first place.

#### `POST /api/auth/register` — Create a new account

```
Body    : { email, password }
Returns : 201 Created   →  { id, email, role }
          409 Conflict  →  email already registered
```

Returns 201 Created on success. 409 Conflict is used for duplicate email because the request is well-formed but conflicts with existing state — more precise than 400.

#### `POST /api/auth/login` — Authenticate and issue tokens

```
Body    : { email, password }
Returns : 200 OK         →  { accessToken, refreshToken, expiresInSeconds }
          401 Unauthorized →  invalid credentials or account suspended
          429 Too Many Requests →  rate limit exceeded
```

Protected by `[EnableRateLimiting("login")]`. Returns a single `InvalidCredentials` error for both "email not found" and "wrong password" to prevent user enumeration. The caller's IP is captured and forwarded to the audit log.

**Dual-token strategy:**
- **Access token** — short-lived JWT (default 60 min). Client stores in memory (not localStorage). Sent as `Authorization: Bearer <token>` on every request.
- **Refresh token** — long-lived opaque string (default 7 days). Client stores in localStorage. Used only to obtain a new access token via `/auth/refresh` without re-entering credentials.

#### `POST /api/auth/refresh` — Exchange refresh token for new token pair

```
Body    : { token }
Returns : 200 OK         →  { accessToken, refreshToken, expiresInSeconds }
          401 Unauthorized →  token invalid, expired, or already revoked
```

No `[Authorize]` — the access token may be expired, which is the exact scenario where this endpoint is needed. The refresh token itself is the credential. Implements **refresh token rotation**: the presented token is revoked and a new pair is issued. A stolen token detected in use alerts the legitimate owner on their next refresh attempt.

#### `POST /api/auth/revoke` — Invalidate a refresh token (explicit logout)

```
Body    : { token }
Returns : 204 No Content →  token revoked
          401 Unauthorized →  caller not authenticated
```

Requires `[Authorize]` — only an authenticated caller can revoke a token. Sets `RevokedAt` on the token row; does **not** revoke the access token (stateless JWTs cannot be revoked without a denylist — a future level concern).

---

### `FilesController` — `/api/files`

Handles all file management operations. `[Authorize]` at the class level — every endpoint requires a valid JWT Bearer token. User identity is read from `User.GetUserId()` (the `NameIdentifier` JWT claim) rather than the Level 0 `X-User-Id` header.

The controller follows a strict, uniform pattern for every endpoint:

```
1. Extract UserId from JWT claim via User.GetUserId()
2. Extract IP address for audit logging
3. Build Command or Query
4. Send via ISender (MediatR)
5. Map Result to IActionResult
```

#### `POST /api/files` — Upload a file

```
Body    : multipart/form-data  { file }
Returns : 201 Created  →  { id, fileName, mimeType, fileSize, uploadedAt }
          400 Bad Request   →  empty file
          413 Payload Too Large  →  file > 25 MB
```

`[RequestSizeLimit(26_214_400)]` enforces the 25 MB cap at the ASP.NET Core level before the handler reads the stream. The handler also checks size as defence-in-depth. On success, returns `201 Created` with a `Location: /api/files/{id}` header (`CreatedAtAction`).

#### `GET /api/files` — List files

```
Returns : 200 OK  →  [ { id, fileName, mimeType, fileSize, uploadedAt }, ... ]
```

Returns only files owned by the authenticated user. An empty array is a valid 200 response.

#### `GET /api/files/{id:guid}` — Download a file

```
Returns : 200 OK        →  raw file bytes (Content-Type set from metadata)
          403 Forbidden  →  file exists but belongs to a different user
          404 Not Found  →  no file with that GUID
```

`{id:guid}` route constraint rejects non-GUID ids at the routing layer. Returns the file stream via `File(stream, mimeType, filename)` — ASP.NET Core streams bytes to the response without buffering the full file in memory.

#### `DELETE /api/files/{id:guid}` — Delete a file

```
Returns : 204 No Content →  deleted
          403 Forbidden  →  file belongs to a different user
          404 Not Found  →  no file with that GUID
```

Returns 403 (not 404) when the file exists but belongs to another user — the file does exist; the caller simply lacks permission.

---

## Extensions

### `ClaimsPrincipalExtensions`

```csharp
public static Guid GetUserId(this ClaimsPrincipal principal)
```

Reads the authenticated user's GUID from the `ClaimTypes.NameIdentifier` claim (which ASP.NET Core maps from the JWT `sub` field). Throws `InvalidOperationException` if the claim is absent — a missing subject claim indicates a bug in `JwtTokenService`, not a normal runtime condition. Centralising this in one extension method means a claim name change requires a single edit.

### `ResultExtensions`

Maps every domain error code to its canonical HTTP status:

| Error | HTTP Status | Reason |
|---|---|---|
| `File.NotFound` | 404 | Resource does not exist |
| `File.Forbidden` | 403 | Resource exists but caller lacks permission |
| `File.TooLarge` | 413 | Payload exceeds size limit |
| `File.Empty` | 400 | Malformed request — no content |
| `User.NotFound` | 404 | User record not found |
| `User.EmailAlreadyExists` | 409 | State conflict — email taken |
| `User.InvalidCredentials` | 401 | Authentication failed |
| `User.Suspended` | 403 | Authenticated but not authorised |
| `Token.Invalid` | 401 | Token unknown or revoked |
| `Token.Expired` | 401 | Token past its expiry time |

Unknown error codes fall back to 500 — a deliberate fail-loud signal that a new domain error is missing a status code mapping.

All errors are returned as RFC 7807 `ProblemDetails`:

```json
{
  "title": "User.InvalidCredentials",
  "detail": "The email or password is incorrect.",
  "status": 401
}
```

---

## `GlobalExceptionMiddleware`

Registered first in the pipeline — wraps everything below it. Catches any unhandled exception that escapes a controller action (database timeout, null reference, programming bug), logs it with full context, and returns a safe `500 Internal Server Error` response.

```csharp
logger.LogError(ex, "Unhandled exception for {Method} {Path}", ...);
```

Structured message templates ensure method and path are indexed as separate fields in log aggregators. The 500 response body never includes the exception message or stack trace:

```json
{
  "title": "Internal Server Error",
  "detail": "An unexpected error occurred. Please try again later.",
  "status": 500
}
```

---

## Design Patterns

### Mediator Pattern (via MediatR)
Controllers dispatch work through `ISender.Send()`. The controller does not know which handler processes a command — it only sends. This decouples the API layer from the application layer and makes adding cross-cutting behaviours (`AuditLoggingBehaviour`) trivial.

### Result Pattern
All operations return `Result` / `Result<T>` instead of throwing exceptions for expected failures. The controller reads `result.IsFailure` and maps to HTTP via `ToProblemResult()` — no try/catch, no exception-driven flow control in happy-path code.

### RFC 7807 Problem Details
Every error response uses the `ProblemDetails` standard. Clients parse errors by `title` (the domain error code) reliably, regardless of which endpoint failed.

### Dual-Token Authentication
Short-lived JWTs (in-memory on client) for request authentication. Long-lived opaque refresh tokens (in localStorage) for session maintenance without re-entering credentials. Rotation on every refresh enables theft detection.

### ISender over IMediator
Controllers inject `ISender` (not `IMediator`) — a narrower interface that exposes only `Send`. This makes the dependency contract explicit: controllers send, they do not publish or otherwise interact with the mediator.

---

## Health Check

`GET /health` returns the live status of the SQL Server connection. Returns `200 Healthy` or `503 Unhealthy` with a JSON body. Used by container orchestrators (Kubernetes liveness/readiness probes) and load balancers to route traffic only to healthy instances.
