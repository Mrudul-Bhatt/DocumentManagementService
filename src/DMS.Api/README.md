# DMS.Api

The entry point of the Document Management Service. This layer owns the HTTP surface — routing, request validation, response shaping, and cross-cutting concerns like exception handling and Swagger documentation. It contains no business logic; its sole responsibility is to translate HTTP into application commands/queries and map results back to HTTP responses.

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

## Project Structure

```
DMS.Api/
├── Controllers/
│   └── FilesController.cs          # HTTP endpoints for all file operations
├── Extensions/
│   └── ResultExtensions.cs         # Maps Result<T> failures to RFC 7807 ProblemDetails
├── Middleware/
│   └── GlobalExceptionMiddleware.cs # Last-resort unhandled exception handler
├── Swagger/
│   └── UserIdHeaderOperationFilter.cs # Injects X-User-Id into every Swagger operation
└── Program.cs                      # Service wiring, middleware pipeline configuration
```

---

## Program.cs — Startup & Middleware Pipeline

`Program.cs` uses the ASP.NET Core minimal hosting model. It configures services and assembles the middleware pipeline.

### Service Registration

| Registration | Purpose |
|---|---|
| `AddApplication()` | Registers MediatR handlers, validators from `DMS.Application` |
| `AddInfrastructure(config)` | Registers EF Core, repositories, file storage from `DMS.Infrastructure` |
| `AddSwaggerGen` + `UserIdHeaderOperationFilter` | Enables Swagger UI with `X-User-Id` header on every operation |
| `AddHealthChecks().AddSqlServer(...)` | Wires a `/health` endpoint that pings SQL Server |

### Middleware Pipeline Order

```
Request
  │
  ├── GlobalExceptionMiddleware      (1st — catches everything below it)
  ├── Swagger / SwaggerUI            (dev only)
  ├── UseHttpsRedirection
  ├── MapControllers                 (routes to FilesController)
  └── MapHealthChecks("/health")
```

Order matters. `GlobalExceptionMiddleware` is registered first so it wraps the entire pipeline — any unhandled exception thrown anywhere below it is caught and returned as a structured error response.

---

## Controllers — `FilesController`

`FilesController` handles all file-related HTTP operations. It follows a strict, uniform pattern for every endpoint:

```
1. Extract X-User-Id from header → fail fast if missing
2. Build a Command or Query object
3. Send it via ISender (MediatR)
4. Map the Result to an IActionResult
```

The controller contains zero business logic. It does not know how files are stored, validated, or retrieved — that knowledge lives in `DMS.Application` and `DMS.Domain`.

### Constructor Injection — Primary Constructor

```csharp
public sealed class FilesController(ISender sender) : ControllerBase
```

Uses the C# 12 primary constructor syntax. `ISender` is the MediatR interface for dispatching commands and queries. Using `ISender` instead of `IMediator` is intentional — `ISender` exposes only `Send`, making the dependency contract narrower and explicit.

### Endpoints

#### `POST /api/files` — Upload a file

```
Headers : X-User-Id (required)
Body    : multipart/form-data  { file }
Returns : 201 Created  →  { id, fileName, mimeType, fileSize, uploadedAt }
          400 Bad Request   →  missing user id or empty file
          413 Payload Too Large  →  file > 25 MB
```

`[RequestSizeLimit(26_214_400)]` enforces the 25 MB cap at the ASP.NET Core level before the request body is even read by the handler — preventing large payloads from consuming memory unnecessarily.

On success, returns `201 Created` with a `Location` header pointing to the download endpoint (`CreatedAtAction`). This follows REST convention: after creating a resource, tell the client where to find it.

#### `GET /api/files` — List files

```
Headers : X-User-Id (required)
Returns : 200 OK  →  [ { id, fileName, mimeType, fileSize, uploadedAt }, ... ]
          400 Bad Request  →  missing user id
```

Returns only files owned by the requesting user. The user identity comes from the header (Level 0 has no real auth — the header is trusted directly).

#### `GET /api/files/{id}` — Download a file

```
Headers : X-User-Id (required)
Returns : 200 OK         →  raw file bytes (Content-Disposition: attachment)
          400 Bad Request →  missing user id
          403 Forbidden   →  file belongs to a different user
          404 Not Found   →  no file with that id
```

Returns the file stream via `File(stream, mimeType, filename)`. ASP.NET Core handles streaming the response correctly — it sets `Content-Type` and `Content-Disposition` headers automatically.

#### `DELETE /api/files/{id}` — Delete a file

```
Headers : X-User-Id (required)
Returns : 204 No Content →  deleted successfully
          403 Forbidden  →  file belongs to a different user
          404 Not Found  →  no file with that id
```

Returns `204 No Content` on success — the REST standard for a successful delete with no body to return.

### Route Constraint `{id:guid}`

Both `GET /{id}` and `DELETE /{id}` use `{id:guid}`. This rejects any request where `id` is not a valid GUID at the routing layer itself, before the handler executes — no manual parsing or validation needed.

---

## `ResultExtensions` — Translating Domain Failures to HTTP

The `Result` pattern (from `DMS.Application`) represents operation outcomes without throwing exceptions. `ResultExtensions.ToProblemResult` converts a failed `Result` into an RFC 7807 `ProblemDetails` HTTP response.

```csharp
private static readonly Dictionary<string, int> ErrorStatusCodes = new()
{
    [DomainErrors.File.NotFound.Code]  = 404,
    [DomainErrors.File.Forbidden.Code] = 403,
    [DomainErrors.File.TooLarge.Code]  = 413,
    [DomainErrors.File.Empty.Code]     = 400,
    [DomainErrors.User.IdMissing.Code] = 400,
};
```

Each domain error code maps to a specific HTTP status code. If a code is unknown (a bug), it defaults to `500` — a safe fallback that doesn't silently swallow the error.

**Why RFC 7807 `ProblemDetails`?**
It's the HTTP standard for machine-readable error responses. Every error response has a consistent shape:

```json
{
  "title": "File.NotFound",
  "detail": "The requested file does not exist.",
  "status": 404
}
```

Clients can reliably parse errors by `title` (the domain error code) rather than scraping message strings.

---

## `GlobalExceptionMiddleware` — Unhandled Exception Handler

Any exception that escapes a controller action — a database timeout, a null reference, a bug — is caught here. It logs the full exception with method and path for debugging, then writes a `500 Internal Server Error` `ProblemDetails` response.

```csharp
logger.LogError(ex, "Unhandled exception for {Method} {Path}", ...);
```

Structured logging with message templates (not string interpolation) ensures the method and path are indexed as separate fields in log aggregators like Seq or Application Insights — not buried inside a flat string.

The error response deliberately hides internal details from the client:

```json
{
  "title": "Internal Server Error",
  "detail": "An unexpected error occurred. Please try again later.",
  "status": 500
}
```

Stack traces and exception messages never reach the HTTP response — doing so would leak implementation details and aid attackers.

---

## `UserIdHeaderOperationFilter` — Swagger Integration

Implements Swashbuckle's `IOperationFilter` to automatically inject an `X-User-Id` header parameter into every operation in the Swagger UI. Without this, testers would have to manually add the header on each request — easy to forget and error-prone during development.

---

## Design Patterns

### Mediator Pattern (via MediatR)
The controller dispatches work through `ISender.Send()` rather than calling application services directly. This decouples the API layer from the application layer — the controller doesn't need to know which service handles an upload, only that something will. It also makes adding cross-cutting behaviours (logging, validation, caching) to the pipeline trivial via `IPipelineBehavior`.

### Result Pattern
Operations return `Result` / `Result<T>` instead of throwing exceptions for expected failures (not found, forbidden, too large). This makes the failure path explicit and typed. The controller reads `result.IsFailure` and maps to HTTP — no try/catch, no exception-driven flow control.

### RFC 7807 Problem Details
All error responses follow the `ProblemDetails` standard. This gives API consumers a consistent, parseable error format regardless of which endpoint failed or why.

### Fail-Fast Validation
Every endpoint checks for `X-User-Id` at the top and returns immediately if missing. Downstream handlers never run with incomplete input — this is the guard clause pattern applied at the HTTP boundary.

---

## Health Check

`GET /health` returns the live status of the SQL Server connection. Returns `200 Healthy` or `503 Unhealthy` with a JSON body. Used by load balancers, container orchestrators (Kubernetes liveness/readiness probes), and monitoring systems to determine if the instance should receive traffic.
