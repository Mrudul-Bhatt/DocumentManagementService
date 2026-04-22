# DMS.Infrastructure

The infrastructure layer is where abstractions meet reality. It provides the concrete implementations of every interface defined in `DMS.Domain` — wiring SQL Server, EF Core, and the local file system into the contracts the domain declared it needs. No other layer knows these implementations exist; they are resolved purely through dependency injection.

---

## Position in the Architecture

```
┌─────────────┐
│   DMS.Api   │
└──────┬──────┘
       │
       ▼
┌──────────────────┐
│ DMS.Application  │  calls IFileMetadataRepository, IFileStorageService
└──────────────────┘
         ▲  (interfaces defined in DMS.Domain)
         │
┌────────┴─────────────┐   ◄── this layer
│  DMS.Infrastructure  │
│                      │
│  FileMetadataRepo    │  implements IFileMetadataRepository
│  LocalFileStorage    │  implements IFileStorageService
│  AppDbContext        │  EF Core unit of work
└──────────────────────┘
         │
         ▼
  SQL Server + Local Disk
```

This layer depends on `DMS.Domain` (to implement its interfaces) and on external frameworks (EF Core, SQL Server). It is the only layer that imports infrastructure-specific NuGet packages.

---

## Project Structure

```
DMS.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs                              # EF Core DbContext — unit of work
│   ├── Configurations/
│   │   └── FileMetadataConfiguration.cs            # Fluent API table and column mappings
│   └── Repositories/
│       └── FileMetadataRepository.cs               # IFileMetadataRepository implementation
├── Storage/
│   └── LocalFileStorageService.cs                  # IFileStorageService implementation (local disk)
├── Migrations/
│   ├── 20260421141810_InitialCreate.cs             # Schema creation migration
│   └── AppDbContextModelSnapshot.cs                # EF Core model snapshot (do not edit manually)
└── DependencyInjection.cs                          # Registers all infrastructure services
```

---

## `DependencyInjection.cs` — Wiring the Layer

```csharp
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

services.AddScoped<IFileMetadataRepository, FileMetadataRepository>();
services.AddScoped<IFileStorageService, LocalFileStorageService>();
```

This is the only place in the entire codebase where infrastructure types are named explicitly. `DMS.Application` and `DMS.Api` only ever reference the domain interfaces. Swapping `LocalFileStorageService` for an S3 implementation or `FileMetadataRepository` for a different ORM requires a change only here.

**`AddScoped` lifetime:**
Both services are registered as `Scoped` — one instance per HTTP request. This aligns with `AppDbContext`, which is also scoped by EF Core's `AddDbContext`. The repository and the context share the same instance within a request, which is required for EF Core's change tracking to work correctly. A `Transient` repository would receive a different `DbContext` than the one managing the transaction.

---

## `AppDbContext` — Unit of Work

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<FileMetadata> FileMetadata => Set<FileMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

`AppDbContext` is the EF Core Unit of Work — it tracks all entity changes within a request and flushes them to the database atomically via `SaveChangesAsync`.

### `DbSet<FileMetadata>` as a Property

`DbSet<FileMetadata>` exposes the `FileMetadata` table as a queryable, trackable collection. Defined as a property returning `Set<FileMetadata>()` rather than a field — this is the recommended pattern in modern EF Core to avoid null reference issues during context initialisation.

### `ApplyConfigurationsFromAssembly`

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
```

Rather than configuring mappings inline in `OnModelCreating`, all entity configurations are discovered automatically from the assembly. Any class implementing `IEntityTypeConfiguration<T>` in `DMS.Infrastructure` is applied automatically. This keeps `OnModelCreating` clean regardless of how many entities are added — a new entity configuration file is picked up with zero changes to `AppDbContext`.

---

## `FileMetadataConfiguration` — Fluent API Mappings

```csharp
internal sealed class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
```

Implements `IEntityTypeConfiguration<FileMetadata>` to define the full SQL schema mapping for the `FileMetadata` entity using EF Core's Fluent API.

**Why Fluent API over Data Annotations?**
Data annotations (`[MaxLength]`, `[Required]`, `[Column]`) are placed directly on the domain entity class. That couples the domain model to EF Core — `DMS.Domain` would need to reference `Microsoft.EntityFrameworkCore`. Fluent API keeps all persistence mapping in the infrastructure layer where it belongs, leaving the domain entity clean.

### Key Configuration Decisions

**`ValueGeneratedNever()` on Id:**
```csharp
builder.Property(f => f.Id).ValueGeneratedNever();
```
EF Core defaults to treating a `Guid` primary key as database-generated (`NEWID()` in SQL Server). Our `FileMetadata.Create` factory generates the `Id` in application code (`Guid.NewGuid()`). `ValueGeneratedNever` tells EF Core not to override it — the value in the entity is always the authoritative one.

**Column max lengths:**
```csharp
builder.Property(f => f.UserId).HasMaxLength(255).IsRequired();
builder.Property(f => f.Filename).HasMaxLength(255).IsRequired();
builder.Property(f => f.MimeType).HasMaxLength(100).IsRequired();
builder.Property(f => f.StoragePath).HasMaxLength(500).IsRequired();
```
Max lengths serve two purposes: they enforce data integrity at the database level, and they make `nvarchar` columns in SQL Server use a bounded type instead of `nvarchar(max)`. Unbounded `nvarchar(max)` columns cannot be indexed efficiently and have higher storage overhead.

**Index on `UserId`:**
```csharp
builder.HasIndex(f => f.UserId).HasDatabaseName("IX_FileMetadata_UserId");
```
`GetByUserIdAsync` filters the entire table by `UserId` on every list request. Without an index, this is a full table scan — O(n) for every user listing their files. With the index, SQL Server can seek directly to the matching rows — O(log n). As the `FileMetadata` table grows this difference is significant. The index is defined here, close to the schema, not buried in a migration.

**`datetimeoffset` for `UploadedAt`:**
EF Core maps `DateTimeOffset` to SQL Server's `datetimeoffset` type, which preserves the UTC offset. This is consistent with the domain entity's choice of `DateTimeOffset` over `DateTime`.

---

## `FileMetadataRepository` — Persistence Implementation

```csharp
internal sealed class FileMetadataRepository(AppDbContext dbContext) : IFileMetadataRepository
```

The concrete implementation of `IFileMetadataRepository`. Uses EF Core to execute all database operations.

### `GetByIdAsync` — `FindAsync` vs `FirstOrDefaultAsync`

```csharp
await dbContext.FileMetadata.FindAsync([id], ct);
```

`FindAsync` checks the EF Core change tracker first — if the entity with that id was already loaded earlier in the same request, it is returned immediately from memory without hitting the database. Only on a cache miss does it issue a `SELECT`. `FirstOrDefaultAsync` always goes to the database regardless.

For a download-then-stream pattern where the same file might be touched more than once in a request, `FindAsync` is the correct choice.

### `GetByUserIdAsync` — Ordering

```csharp
dbContext.FileMetadata
    .Where(f => f.UserId == userId)
    .OrderByDescending(f => f.UploadedAt)
    .ToListAsync(ct);
```

Results are ordered by `UploadedAt` descending — most recently uploaded files appear first. This is the expected UX default for a file list. Ordering is applied in the SQL query (translated to `ORDER BY UploadedAt DESC`), not in memory after fetching — the database sorts efficiently using the data already in storage.

### `AddAsync` and `DeleteAsync` — `SaveChangesAsync` per Operation

```csharp
await dbContext.FileMetadata.AddAsync(file, ct);
await dbContext.SaveChangesAsync(ct);
```

Each mutating operation calls `SaveChangesAsync` immediately. At Level 0 with single-entity operations there is no benefit to batching. `SaveChangesAsync` wraps the operation in a database transaction automatically — either the `INSERT` or `DELETE` fully succeeds or fully rolls back. No partial state.

**`Remove` does not need `await`:**
```csharp
dbContext.FileMetadata.Remove(file);
await dbContext.SaveChangesAsync(ct);
```
`Remove` only marks the entity as `Deleted` in the change tracker — it is synchronous and performs no I/O. The actual `DELETE` SQL is issued by `SaveChangesAsync`.

---

## `LocalFileStorageService` — File System Implementation

```csharp
internal sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
```

The concrete implementation of `IFileStorageService` using the local disk. At a future level this will be replaced by an S3 or Azure Blob Storage implementation — the application and domain layers will not change.

### Storage Layout

Files are stored under a user-scoped directory:

```
uploads/
└── {userId}/
    └── {fileId}       ← no extension, identified by GUID
```

Scoping by `userId` keeps files namespaced on disk, mirrors the ownership model in the database, and makes it straightforward to list or wipe all files for a given user without a database query if ever needed.

The file on disk is named by `fileId` (a GUID), not by the original filename. This avoids:
- Filename collisions when two users upload files with the same name.
- Path traversal vulnerabilities from attacker-controlled filenames containing `../`.
- Character encoding issues from filenames with special characters.

The original filename is preserved in the `FileMetadata` database row for display purposes only.

### `SaveAsync` — Writing the File

```csharp
Directory.CreateDirectory(directory);
var storagePath = Path.Combine(directory, fileId);

await using var fileStream = new FileStream(
    storagePath, FileMode.Create, FileAccess.Write, FileShare.None);
await content.CopyToAsync(fileStream, ct);

return storagePath;
```

**`Directory.CreateDirectory` is idempotent** — it does nothing if the directory already exists. No need to check existence first; calling it unconditionally is safe.

**`FileMode.Create`** overwrites the file if it somehow already exists. Since `fileId` is a fresh GUID this should never happen, but `Create` is safer than `CreateNew` (which would throw on a collision).

**`FileShare.None`** prevents any other process from opening the file while it is being written. This avoids a partial read of a file that is still being uploaded.

**`await using`** ensures the `FileStream` is disposed and flushed even if `CopyToAsync` throws — critical for releasing the file handle and ensuring all bytes are written to disk before `storagePath` is returned to the caller.

**`CopyToAsync`** streams the request body directly from the HTTP request into the file — constant memory use regardless of file size. The upload is not buffered in memory first.

### `ReadAsync` — Opening the File Stream

```csharp
Stream stream = new FileStream(storagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
return Task.FromResult(stream);
```

**`FileShare.Read`** allows multiple concurrent readers on the same file — multiple users could theoretically download the same shared file simultaneously without contention.

The stream is returned open to the caller (the query handler), which passes it to ASP.NET Core to stream directly to the HTTP response. The framework closes the stream after the response completes. This is why `ReadAsync` does not use `await using` — closing the stream here would make it unreadable by the time the controller tries to write it.

**`Task.FromResult`** wraps the synchronous result in a completed `Task` to satisfy the async interface contract without allocating a state machine. The file open operation itself is synchronous; only the subsequent streaming to the client is async.

The `File.Exists` check before opening throws a descriptive `FileNotFoundException` if the storage path in the database points to a missing file — a defensive guard against database/disk getting out of sync (e.g. a file manually deleted from disk).

### `DeleteAsync` — Removing the File

```csharp
if (File.Exists(storagePath))
    File.Delete(storagePath);
return Task.CompletedTask;
```

The existence check makes deletion idempotent — if the file was already removed (e.g. a previous failed request partially succeeded), the operation succeeds silently rather than throwing. This is important for retry safety: if the caller retries a failed delete, the second attempt must not fail because the first attempt already removed the file.

`Task.CompletedTask` returns a cached, already-completed `Task` — no allocation. Used for the same reason as `Task.FromResult` above: the operation is synchronous but the interface is async.

---

## Migrations

EF Core migrations are the version-controlled history of the database schema. Each migration is a C# class with `Up` (apply) and `Down` (rollback) methods. They are applied via:

```bash
dotnet ef database update --project src/DMS.Infrastructure --startup-project src/DMS.Api
```

### `InitialCreate` Migration

Creates the `FileMetadata` table with all columns and the `IX_FileMetadata_UserId` index. The `Down` method drops the table entirely — a clean rollback to an empty database.

### `AppDbContextModelSnapshot`

EF Core maintains a snapshot of the current model state in `AppDbContextModelSnapshot.cs`. When a new migration is scaffolded (`dotnet ef migrations add`), EF Core diffs the current domain model against this snapshot to generate only the incremental changes. This file is managed entirely by EF Core — never edit it manually.

---

## `internal sealed` — Visibility of Implementations

All infrastructure implementations (`FileMetadataRepository`, `LocalFileStorageService`, `FileMetadataConfiguration`) are `internal sealed`:

- **`internal`** — these types are implementation details of `DMS.Infrastructure`. No other assembly can reference them directly. `DMS.Application` and `DMS.Api` receive them as the domain interface type through the DI container — they cannot import or instantiate the concrete classes.
- **`sealed`** — these classes are not designed for inheritance. They have a single, specific job. Sealing prevents accidental subclassing and signals that the class is a leaf in the type hierarchy.
