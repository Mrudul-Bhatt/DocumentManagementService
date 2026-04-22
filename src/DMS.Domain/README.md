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

This is the Dependency Inversion Principle in action. `DMS.Domain` defines interfaces (`IFileMetadataRepository`, `IFileStorageService`) that describe what it needs. `DMS.Infrastructure` provides the concrete implementations. The domain never imports the infrastructure — the dependency arrow points inward, always.

---

## Project Structure

```
DMS.Domain/
├── Entities/
│   └── FileMetadata.cs               # The core domain entity
├── Errors/
│   └── DomainErrors.cs               # All named domain failures + the Error record
├── Repositories/
│   └── IFileMetadataRepository.cs    # Contract for metadata persistence
└── Services/
    └── IFileStorageService.cs        # Contract for binary file storage
```

---

## `FileMetadata` — The Domain Entity

```csharp
public sealed class FileMetadata
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; }
    public string Filename { get; private set; }
    public long FileSize { get; private set; }
    public string MimeType { get; private set; }
    public string StoragePath { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
}
```

`FileMetadata` represents a stored file in the domain. It owns the identity, ownership, and descriptive data for a file. It is the single source of truth for answering "does this user own this file?" and "where is this file stored?".

### Private Setters — Encapsulation

Every property has a `private set`. Properties cannot be assigned from outside the class — not by EF Core model binding, not by application handlers, not by tests. The only way to put `FileMetadata` into a valid state is through the controlled entry points the class itself provides.

This prevents the entity from being put into an inconsistent state (e.g. a `FileMetadata` with no `UserId`, or a future entity where two properties must always be set together).

EF Core is designed to work with private setters — it uses reflection to populate properties when materialising entities from the database, bypassing the setter visibility.

### Private Parameterless Constructor

```csharp
private FileMetadata() { }
```

EF Core requires a parameterless constructor to materialise entities from database query results. Making it `private` means application code cannot call `new FileMetadata()` and get an empty, uninitialised instance. The only valid way to construct a new `FileMetadata` from application code is via the `Create` factory method.

### `Create` — Static Factory Method

```csharp
public static FileMetadata Create(
    string userId,
    string filename,
    long fileSize,
    string mimeType,
    string storagePath)
```

Rather than a public constructor, `FileMetadata` uses a static factory method. This is the canonical pattern in Domain-Driven Design for entity construction.

**Why a factory method over a public constructor?**
- It has a meaningful name (`Create`) that expresses intent — you are creating a new file record, not just allocating memory.
- It centralises all initialisation logic: generating the `Id`, setting `UploadedAt` to `UtcNow`. If this logic ever becomes more complex (e.g. raising a domain event on creation), the change is made in one place.
- `UploadedAt = DateTimeOffset.UtcNow` is set here, not by the caller — the domain controls its own timestamps. Application code cannot pass an arbitrary past or future timestamp.
- `Guid.NewGuid()` for `Id` is generated here — the domain controls its own identity.

### `BelongsTo` — Ownership Check as a Domain Method

```csharp
public bool BelongsTo(string userId) => UserId == userId;
```

This is the most important method on the entity, despite being one line. It encodes an ownership rule: *a file belongs to the user who uploaded it*.

**Why not just compare `metadata.UserId == command.UserId` in the handler?**
- If you scatter that comparison across every handler that needs it, the rule exists in multiple places. When the rule changes (e.g. a file can belong to an organisation, not just a user), you have to find and update every occurrence.
- Putting the rule on the entity means it lives in exactly one place — the entity that owns the data the rule is about. Handlers call `BelongsTo`; they don't know how ownership is determined.
- This is the Tell, Don't Ask principle: instead of asking the entity for its data and making the decision externally, you tell the entity to make the decision itself.

### `sealed` Class

`FileMetadata` is `sealed` — it cannot be subclassed. Domain entities are not designed for inheritance; they model specific real-world concepts. Sealing it prevents accidental extension and makes the type's behaviour fully predictable.

### `DateTimeOffset` vs `DateTime`

`UploadedAt` uses `DateTimeOffset`, not `DateTime`. `DateTimeOffset` stores the UTC offset alongside the time value, making it unambiguous regardless of the server's local timezone. `DateTime` with `Kind = Utc` is technically equivalent but is easier to misuse — casting to `DateTimeKind.Local` silently corrupts the value. In a distributed system where servers may run in different regions, `DateTimeOffset.UtcNow` is always correct.

---

## `DomainErrors` — Named Error Catalogue

```csharp
public static class DomainErrors
{
    public static class File
    {
        public static readonly Error NotFound  = new("File.NotFound",  "The requested file does not exist.");
        public static readonly Error Forbidden = new("File.Forbidden", "You do not have permission to access this file.");
        public static readonly Error TooLarge  = new("File.TooLarge",  "File exceeds the maximum allowed size of 25 MB.");
        public static readonly Error Empty     = new("File.Empty",     "Uploaded file cannot be empty.");
    }

    public static class User
    {
        public static readonly Error IdMissing = new("User.IdMissing", "The X-User-Id header is required.");
    }
}
```

All named failures in the system are defined here as static readonly fields — a single catalogue of every error the domain can produce.

**Why a central catalogue over inline strings?**
- Errors are referenced by multiple layers (application handlers return them, the API maps them to HTTP status codes). A central definition means one change propagates everywhere.
- Each error has a `Code` (`"File.NotFound"`) and a `Description`. The code is a stable, machine-readable identifier that the API layer uses to map to an HTTP status code. The description is the human-readable message.
- Grouping errors in nested static classes (`DomainErrors.File`, `DomainErrors.User`) provides discoverability — a developer typing `DomainErrors.` in their IDE sees all available error categories.

### `Error` Record

```csharp
public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}
```

`Error` is a `record` — it has value equality by default. Two `Error` instances with the same `Code` and `Description` are considered equal. This is used by `Result`'s constructor guard:

```csharp
if (isSuccess && error != Error.None)
    throw new InvalidOperationException(...);
```

`Error.None` is the sentinel value representing "no error" — used only on the success path of a `Result`. It is defined on `Error` itself, not `DomainErrors`, because it is not a domain failure; it is the absence of one.

---

## `IFileMetadataRepository` — Persistence Contract

```csharp
public interface IFileMetadataRepository
{
    Task<FileMetadata?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FileMetadata>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task AddAsync(FileMetadata file, CancellationToken ct = default);
    Task DeleteAsync(FileMetadata file, CancellationToken ct = default);
}
```

This interface is the Repository pattern. It defines what the application needs from persistent storage in terms of the domain model (`FileMetadata`), with no mention of SQL, EF Core, tables, or queries. The concrete implementation (`FileMetadataRepository` in `DMS.Infrastructure`) is the only place those details exist.

**Why the Repository pattern?**
- Application handlers are written against this interface. They can be tested by substituting a fake in-memory implementation — no database required for unit tests.
- Swapping the database engine (SQL Server → PostgreSQL) requires changing only the infrastructure implementation, not the application logic.
- The interface expresses the domain's needs in domain terms: "give me a `FileMetadata` by id", not "execute `SELECT * FROM FileMetadata WHERE Id = @id`".

**`GetByIdAsync` returns `FileMetadata?` (nullable)**
The `?` is a deliberate contract: the repository explicitly declares that a file may not exist. Handlers must handle the null case — the compiler enforces this under nullable reference types. Returning null is preferable to throwing `FileNotFoundException` because "not found" is an expected outcome, not an exceptional one.

**`DeleteAsync` accepts the entity, not an id**
```csharp
Task DeleteAsync(FileMetadata file, CancellationToken ct = default);
```
The handler fetches the entity first (to check it exists and is owned by the user), then passes the loaded entity to `DeleteAsync`. This avoids a second database lookup inside the repository and means EF Core can use the already-tracked entity for deletion — no redundant `SELECT`.

---

## `IFileStorageService` — Storage Contract

```csharp
public interface IFileStorageService
{
    Task<string> SaveAsync(Stream content, string userId, string fileId, CancellationToken ct = default);
    Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default);
    Task DeleteAsync(string storagePath, CancellationToken ct = default);
}
```

This is a Domain Service interface — it represents a capability the domain requires that is not naturally modelled as a method on an entity. Storing raw bytes is an infrastructure concern; declaring the need for it is a domain concern.

**Why a domain service interface and not just call storage directly?**
- At Level 0, the implementation is local disk. At a later level it will be S3 or Azure Blob Storage. The domain and application layers will not change — only the infrastructure implementation is swapped.
- Like the repository, this interface allows the application layer to be tested without a real file system.

**`SaveAsync` returns `string` (the storage path)**
The storage service decides the physical path (e.g. `uploads/user123/file-abc.pdf`). The domain does not dictate how files are laid out on disk or in a bucket — that is an infrastructure decision. The returned `storagePath` is stored in `FileMetadata` and passed back to the service for future reads and deletes. The domain treats it as an opaque handle.

**`ReadAsync` returns `Stream`**
Returns a stream rather than `byte[]` for the same reason as in the application layer — files can be large, and streaming avoids loading the entire file into memory at once.

---

## Design Philosophy — Why the Domain Has No Dependencies

The domain layer imports no NuGet packages beyond the .NET base class library. This is not accidental — it is the core principle of Clean Architecture and Domain-Driven Design.

- **Testability:** Domain logic can be tested with plain unit tests. No database, no HTTP, no file system needed.
- **Longevity:** Infrastructure frameworks change (ORMs, cloud SDKs, HTTP libraries). The domain model describes the business, which changes far more slowly. Keeping them separate means framework upgrades don't require rewriting business logic.
- **Clarity:** When a developer opens `DMS.Domain`, they see only business concepts — entities, rules, errors, contracts. There is no noise from persistence annotations, HTTP attributes, or serialisation concerns.

The dependency rule is: source code dependencies always point inward. The domain is the innermost layer and depends on nothing outside itself.
