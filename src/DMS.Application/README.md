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

`DMS.Application` defines what it needs (`IFileMetadataRepository`, `IFileStorageService`) but never knows how those are implemented. The DI container wires the concrete implementations at startup.

---

## Project Structure

```
DMS.Application/
├── Common/
│   └── Result.cs                                  # Result / Result<T> — typed outcome wrapper
├── DTOs/
│   ├── FileMetadataDto.cs                         # Read model for file metadata
│   └── FileDownloadResult.cs                      # Read model for file download stream
├── Files/
│   ├── Commands/
│   │   ├── UploadFile/
│   │   │   ├── UploadFileCommand.cs               # Input for upload operation
│   │   │   └── UploadFileCommandHandler.cs        # Upload use case logic
│   │   └── DeleteFile/
│   │       ├── DeleteFileCommand.cs               # Input for delete operation
│   │       └── DeleteFileCommandHandler.cs        # Delete use case logic
│   └── Queries/
│       ├── ListFiles/
│       │   ├── ListFilesQuery.cs                  # Input for list operation
│       │   └── ListFilesQueryHandler.cs           # List use case logic
│       └── DownloadFile/
│           ├── DownloadFileQuery.cs               # Input for download operation
│           └── DownloadFileQueryHandler.cs        # Download use case logic
└── DependencyInjection.cs                         # Registers MediatR with the DI container
```

---

## Core Patterns

### CQRS — Command Query Responsibility Segregation

Every operation is classified as either a **Command** (mutates state, returns success/failure) or a **Query** (reads state, returns data, never mutates).

| Type | Operations | Returns |
|---|---|---|
| Command | Upload, Delete | `Result` / `Result<T>` |
| Query | List, Download | `Result<T>` (read model) |

This separation matters because reads and writes have different concerns. Reads need to be fast and can be optimised independently (caching, read replicas). Writes need to be correct and atomic. Mixing them in a single service method creates methods that do too much and are harder to reason about.

In this layer, CQRS is implemented via MediatR's `IRequest<TResponse>` marker interface — a Command or Query is just a record that implements `IRequest<Result<T>>`. MediatR routes it to the correct handler.

### Mediator Pattern (via MediatR)

Rather than the API layer calling handlers directly, it sends a message to MediatR (`ISender.Send(command)`), which resolves and invokes the correct `IRequestHandler`. This decouples the caller from the handler completely — the controller doesn't import or instantiate `UploadFileCommandHandler`; it only knows about `UploadFileCommand`.

**Why this matters at scale:** Pipeline behaviours (`IPipelineBehavior<TRequest, TResponse>`) can be inserted between the sender and the handler without changing either. Future levels will add validation, logging, and caching as pipeline behaviours — zero changes to existing handlers.

### Result Pattern

Operations return `Result` or `Result<TValue>` instead of throwing exceptions for expected failures (file not found, wrong owner, too large). This is the most important pattern in this layer.

```
Success path:  Result.Success(value)      →  IsSuccess = true,  Value = value
Failure path:  Result.Failure(error)      →  IsFailure = true,  Error = { Code, Description }
```

**Why not just throw?**
- Exceptions are for unexpected failures (bugs, infrastructure outages). "File not found" is an expected outcome, not exceptional.
- Throwing and catching exceptions for control flow is expensive (stack unwinding) and obscures intent.
- With `Result`, every handler's signature declares its failure modes explicitly — callers are forced to handle them.
- The entire call chain from handler → controller stays exception-free for known failure cases.

`Result<TValue>.Value` throws `InvalidOperationException` if accessed on a failed result — a deliberate guard that prevents callers from ignoring `IsFailure`.

---

## Commands

### `UploadFileCommand` / `UploadFileCommandHandler`

**Input:**
```csharp
record UploadFileCommand(
    string UserId,
    string Filename,
    string MimeType,
    long FileSize,
    Stream Content)
```

**What the handler does (in order):**
1. Validates file is not empty (`FileSize == 0`)
2. Validates file does not exceed 25 MB
3. Generates a new `fileId` (GUID) for the storage path
4. Saves the raw bytes via `IFileStorageService.SaveAsync`
5. Creates a `FileMetadata` domain entity via `FileMetadata.Create`
6. Persists the metadata via `IFileMetadataRepository.AddAsync`
7. Logs the upload event with structured fields
8. Returns a `FileMetadataDto` on success

**Key decisions:**
- Validation happens before touching storage or the database. If the file is too large, no I/O occurs.
- Storage and metadata are separate operations. `storagePath` returned by the storage service is stored in the metadata row — it's the link between the database record and the physical file.
- The handler is `internal sealed` — it is not part of the public API of this assembly. Only MediatR (via reflection at registration time) needs to discover it. Callers interact through the command record, never the handler directly.

### `DeleteFileCommand` / `DeleteFileCommandHandler`

**Input:**
```csharp
record DeleteFileCommand(Guid FileId, string UserId)
```

**What the handler does (in order):**
1. Loads the metadata record by `FileId`
2. Returns `File.NotFound` if no record exists
3. Calls `metadata.BelongsTo(userId)` — an ownership check on the domain entity
4. Returns `File.Forbidden` if the caller doesn't own the file
5. Deletes the physical file via `IFileStorageService.DeleteAsync`
6. Deletes the metadata row via `IFileMetadataRepository.DeleteAsync`
7. Logs the deletion
8. Returns `Result.Success()`

**Key decision — ownership check on the domain entity:**
`BelongsTo` is a method on `FileMetadata` (the domain entity), not implemented here in the handler. Business rules that involve an entity's own data belong on the entity. The handler orchestrates; the entity enforces its own invariants.

**Order of deletion matters:** Physical file is deleted before the database row. If the DB delete fails after the file is gone, the row becomes an orphan pointing to a missing file. The alternative (DB first) leaves an unreachable orphan file on disk. Neither is perfect without a transaction spanning both systems — this is an acceptable trade-off at Level 0 with local storage.

---

## Queries

### `ListFilesQuery` / `ListFilesQueryHandler`

**Input:**
```csharp
record ListFilesQuery(string UserId)
```

**What the handler does:**
1. Fetches all metadata rows for the user via `IFileMetadataRepository.GetByUserIdAsync`
2. Projects each `FileMetadata` entity to a `FileMetadataDto`
3. Returns the list as `IReadOnlyList<FileMetadataDto>`

**Why `IReadOnlyList` and not `IEnumerable` or `List`?**
- `IReadOnlyList` signals to the caller that the collection is fully materialised (no deferred execution) and should not be mutated.
- `IEnumerable` would leave open the question of whether iterating it hits the database again.
- `List` exposes mutation methods (`Add`, `Remove`) that have no business being on a read result.

`.ToList().AsReadOnly()` materialises the query results into memory and wraps in a read-only adapter — safe to iterate multiple times with no deferred execution.

### `DownloadFileQuery` / `DownloadFileQueryHandler`

**Input:**
```csharp
record DownloadFileQuery(Guid FileId, string UserId)
```

**What the handler does:**
1. Loads the metadata record by `FileId`
2. Returns `File.NotFound` if missing
3. Checks ownership via `metadata.BelongsTo(userId)`
4. Returns `File.Forbidden` if not the owner
5. Opens the file stream via `IFileStorageService.ReadAsync`
6. Returns a `FileDownloadResult` wrapping the stream, filename, and MIME type

**Why return a `Stream` and not `byte[]`?**
Files can be large (up to 25 MB). Loading the entire file into a `byte[]` allocates it in memory all at once. Returning a `Stream` lets ASP.NET Core stream the response directly to the client in chunks — constant memory use regardless of file size.

---

## DTOs — Data Transfer Objects

DTOs are the data shapes that cross the boundary between the application layer and the API layer. Domain entities (`FileMetadata`) are never exposed directly to callers.

**Why not expose the entity directly?**
- Entities may have internal state, navigation properties, or methods that are not relevant to an API response.
- Exposing entities couples the API contract to the domain model — a refactor inside the domain would break the API response shape.
- DTOs are `sealed record` types — immutable, value-equality by default, and structurally minimal.

### `FileMetadataDto`

```csharp
sealed record FileMetadataDto(Guid Id, string Filename, long FileSize, string MimeType, DateTimeOffset UploadedAt)
```

Returned by `UploadFile` and `ListFiles`. Contains exactly what the API consumer needs — no storage paths, no internal identifiers, no ORM navigation properties.

### `FileDownloadResult`

```csharp
sealed record FileDownloadResult(Stream Content, string Filename, string MimeType)
```

Wraps the file stream with the metadata needed to set correct HTTP response headers (`Content-Type`, `Content-Disposition`). The controller unpacks this directly into `File(stream, mimeType, filename)`.

---

## `DependencyInjection.cs`

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
```

Scans the entire `DMS.Application` assembly and registers every class implementing `IRequestHandler<,>` with the DI container. This is the only place MediatR needs to be configured for this layer. Adding a new handler requires no changes here — it is discovered automatically.

`typeof(DependencyInjection)` is used as the assembly anchor — it's a stable type that lives in this assembly and will never be moved.

---

## Handler Visibility — `internal sealed`

All handlers are `internal sealed`:

- **`internal`** — handlers are an implementation detail of this assembly. No external code should reference `UploadFileCommandHandler` directly. The contract is the command record, which is `public`.
- **`sealed`** — handlers are not designed for inheritance. Sealing prevents accidental subclassing and allows the JIT to devirtualise method calls.

MediatR resolves handlers via the DI container using reflection, so `internal` visibility is not an obstacle.

---

## CancellationToken Propagation

Every handler method accepts a `CancellationToken ct` and passes it through to every async I/O call (`repository`, `storageService`). This is non-negotiable in production systems.

If a client disconnects mid-request, ASP.NET Core cancels the token. Without propagation, the handler would continue writing to disk and the database even though no one is waiting for the result — wasting resources under load.
