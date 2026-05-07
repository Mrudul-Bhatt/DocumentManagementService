# Level 2 — Folders + Versioning

> **Problem:** Users have hundreds of files with no organisation, and no way to recover overwritten files.

Full requirements: [`dms_requirements.txt`](dms_requirements.txt)

---

## Functional Requirements

### Folders
- Create, rename, move, and delete folders (arbitrary nesting depth)
- Move files between folders
- Soft-delete files and folders → Trash; restore within 30 days
- Trash auto-purge runs nightly (hard-deletes items older than 30 days)

### Versioning
- Uploading a file with the same name into the same folder creates a new version — no silent overwrite
- List all versions of a file: version number, size, uploader, timestamp
- Restore any prior version as the current version
- Hard-delete a specific version permanently

## Non-Functional Requirements

| Constraint | Value |
|---|---|
| Folder nesting depth | Max 20 levels |
| Version retention | 90 days by default (configurable per plan) |
| Folder delete | Must be atomic — all descendants deleted in a single transaction |
| Trash auto-purge | Runs nightly |

## Key Technologies
Adjacency list (SQL Server recursive CTEs), Soft delete (`DeletedAt` nullable timestamp), Version table, `IHostedService` background worker

---

## DMS.Domain

### New Entities

#### `Folder`
| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Application-generated |
| `Name` | `string` | Display name |
| `OwnerId` | `Guid` | FK to User |
| `ParentFolderId` | `Guid?` | Null = root folder; self-referencing FK |
| `DeletedAt` | `DateTimeOffset?` | Null = active; set = soft-deleted (in Trash) |
| `CreatedAt` | `DateTimeOffset` | UTC creation timestamp |

Domain methods:
- `Create(name, ownerId, parentFolderId?)` — static factory
- `Rename(newName)` — updates Name
- `SoftDelete()` — sets `DeletedAt = UtcNow`
- `Restore()` — clears `DeletedAt`
- `BelongsTo(userId)` — ownership check

#### `FileVersion`
| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Application-generated |
| `FileId` | `Guid` | FK to `FileMetadata` |
| `VersionNumber` | `int` | 1-based; monotonically increasing per file |
| `StoragePath` | `string` | Opaque path to physical bytes |
| `FileSize` | `long` | Bytes |
| `UploadedBy` | `string` | UserId of uploader |
| `CreatedAt` | `DateTimeOffset` | UTC timestamp of this version |

Domain methods:
- `Create(fileId, versionNumber, storagePath, fileSize, uploadedBy)` — static factory

#### `FileMetadata` — Updated
New properties added:
- `FolderId` — `Guid?` — null = root (no folder)
- `DeletedAt` — `DateTimeOffset?` — null = active; set = soft-deleted

New domain methods:
- `SoftDelete()` — sets `DeletedAt = UtcNow`
- `Restore()` — clears `DeletedAt`
- `MoveTo(folderId?)` — updates `FolderId`

### New Repository Interfaces

#### `IFolderRepository`
```
GetByIdAsync(id)                          → Folder?
GetRootFoldersAsync(ownerId)              → IReadOnlyList<Folder>
GetChildrenAsync(parentFolderId, ownerId) → IReadOnlyList<Folder>
GetDescendantIdsAsync(folderId)           → IReadOnlyList<Guid>   (recursive CTE)
AddAsync(folder)
UpdateAsync(folder)
DeleteAsync(folder)
```

#### `IFileVersionRepository`
```
GetByIdAsync(id)                 → FileVersion?
GetByFileIdAsync(fileId)         → IReadOnlyList<FileVersion>   (all versions, desc)
GetLatestVersionNumberAsync(fileId) → int
AddAsync(version)
DeleteAsync(version)
```

### New Domain Errors

#### `DomainErrors.Folder`
| Error | Code | HTTP |
|---|---|---|
| `NotFound` | `Folder.NotFound` | 404 |
| `Forbidden` | `Folder.Forbidden` | 403 |
| `MaxDepthExceeded` | `Folder.MaxDepthExceeded` | 400 |
| `NameConflict` | `Folder.NameConflict` | 409 |

#### `DomainErrors.FileVersion`
| Error | Code | HTTP |
|---|---|---|
| `NotFound` | `FileVersion.NotFound` | 404 |
| `CannotDeleteCurrent` | `FileVersion.CannotDeleteCurrent` | 400 |

---

## DMS.Application

### New DTOs

| DTO | Contents | Used By |
|---|---|---|
| `FolderDto` | `Id, Name, ParentFolderId, CreatedAt` | All folder queries |
| `FolderContentsDto` | `IReadOnlyList<FolderDto> Folders, IReadOnlyList<FileMetadataDto> Files` | GetFolderContents |
| `FileVersionDto` | `Id, VersionNumber, FileSize, UploadedBy, CreatedAt` | ListFileVersions |
| `TrashedItemDto` | `Id, Name, Type (File/Folder), DeletedAt` | ListTrash |

### New Folder Commands and Queries

| Operation | Type | Input | Returns |
|---|---|---|---|
| `CreateFolderCommand` | Command | `Name, ParentFolderId?, UserId, IpAddress?` | `Result<FolderDto>` |
| `RenameFolderCommand` | Command | `FolderId, NewName, UserId, IpAddress?` | `Result` |
| `MoveFolderCommand` | Command | `FolderId, TargetParentFolderId?, UserId` | `Result` |
| `DeleteFolderCommand` | Command | `FolderId, UserId, IpAddress?` | `Result` |
| `RestoreFolderCommand` | Command | `FolderId, UserId` | `Result` |
| `GetFolderContentsQuery` | Query | `FolderId?, UserId` | `Result<FolderContentsDto>` |

### New Versioning Commands and Queries

| Operation | Type | Input | Returns |
|---|---|---|---|
| `ListFileVersionsQuery` | Query | `FileId, UserId` | `Result<IReadOnlyList<FileVersionDto>>` |
| `RestoreFileVersionCommand` | Command | `FileId, VersionId, UserId, IpAddress?` | `Result<FileMetadataDto>` |
| `DeleteFileVersionCommand` | Command | `FileId, VersionId, UserId` | `Result` |

### New Trash Commands and Queries

| Operation | Type | Input | Returns |
|---|---|---|---|
| `ListTrashQuery` | Query | `UserId` | `Result<IReadOnlyList<TrashedItemDto>>` |
| `RestoreFromTrashCommand` | Command | `ItemId, ItemType (File/Folder), UserId` | `Result` |
| `EmptyTrashCommand` | Command | `UserId` | `Result` |

### Updated File Commands

**`UploadFileCommand`** — adds `FolderId?` parameter

Handler logic change:
1. If `FolderId` is provided, verify the folder exists and the user owns it
2. Check whether a file with the same `Filename` already exists in that folder (or root)
3. If **no existing file** → create new `FileMetadata` (same as Level 1) + create `FileVersion` with `VersionNumber = 1`
4. If **existing file found** → create new `FileVersion` with `VersionNumber = MAX + 1`; update `FileMetadata.StoragePath` to point to the new version's bytes

**`DeleteFileCommand`** — changes to soft-delete
- Instead of hard-deleting: calls `metadata.SoftDelete()` + `UpdateAsync`
- Physical file and metadata row are NOT removed — moved to Trash

### New Background Service Interface

#### `ITrashPurgeService`
```
PurgeExpiredItemsAsync(retentionDays, ct) → Task
```
Implemented in Infrastructure as a nightly `IHostedService`. Queries all soft-deleted files and folders where `DeletedAt < UtcNow - retentionDays`. Hard-deletes physical storage files, then hard-deletes DB rows.

### `DependencyInjection.cs` — Changes
- Register `IFolderRepository`, `IFileVersionRepository` as Scoped
- Register `TrashPurgeBackgroundService` via `AddHostedService<TrashPurgeBackgroundService>()`

---

## DMS.Infrastructure

### New EF Configurations

#### `FolderConfiguration`
- `ToTable("Folders")`
- `ValueGeneratedNever()` on Id
- `HasMaxLength(255)` on Name
- `HasIndex(f => f.OwnerId)` → `IX_Folders_OwnerId`
- `HasIndex(f => f.ParentFolderId)` → `IX_Folders_ParentFolderId`
- Self-referencing FK: `HasOne(f => f.Parent).WithMany(f => f.Children).HasForeignKey(f => f.ParentFolderId)`
- Global query filter: `HasQueryFilter(f => f.DeletedAt == null)` — soft-deleted folders excluded automatically

#### `FileVersionConfiguration`
- `ToTable("FileVersions")`
- `ValueGeneratedNever()` on Id
- `HasMaxLength(500)` on StoragePath
- `HasIndex(v => v.FileId)` → `IX_FileVersions_FileId`
- FK to `FileMetadata`: `HasOne(...).WithMany(...).HasForeignKey(v => v.FileId)`

#### `FileMetadataConfiguration` — Updated
- Add `FolderId` nullable column
- Add `DeletedAt` nullable column
- `HasIndex(f => f.FolderId)` → `IX_FileMetadata_FolderId`
- Global query filter updated: `HasQueryFilter(f => f.DeletedAt == null)`

### New Repository Implementations

#### `FolderRepository`
- `GetDescendantIdsAsync` — executes a recursive CTE via EF Core raw SQL:
  ```sql
  WITH FolderTree AS (
      SELECT Id FROM Folders WHERE Id = @rootId
      UNION ALL
      SELECT f.Id FROM Folders f
      INNER JOIN FolderTree ft ON f.ParentFolderId = ft.Id
  )
  SELECT Id FROM FolderTree
  ```
- All other methods use standard EF Core LINQ

#### `FileVersionRepository`
- `GetByFileIdAsync` — orders by `VersionNumber DESC`
- `GetLatestVersionNumberAsync` — `MAX(VersionNumber)` query; returns 0 if no versions exist

#### `TrashPurgeBackgroundService`
- Implements `BackgroundService` (inherits from `IHostedService`)
- Uses `PeriodicTimer` with a 24-hour interval
- On each tick: loads expired soft-deleted items, deletes physical files via `IFileStorageService`, hard-deletes DB rows

### New Migration

#### `AddFoldersAndVersioning`
Creates:
- `Folders` table with self-referencing FK and indexes
- `FileVersions` table with FK to `FileMetadata` and index
- Adds `FolderId` and `DeletedAt` columns to `FileMetadata`

---

## DMS.Api

### New Controller — `FoldersController` (`/api/folders`)

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/folders` | Create a folder |
| `PUT` | `/api/folders/{id:guid}/name` | Rename a folder |
| `PUT` | `/api/folders/{id:guid}/parent` | Move a folder |
| `DELETE` | `/api/folders/{id:guid}` | Soft-delete a folder (to Trash) |
| `POST` | `/api/folders/{id:guid}/restore` | Restore a folder from Trash |
| `GET` | `/api/folders/{id:guid}/contents` | List immediate children (subfolders + files) |
| `GET` | `/api/folders/root` | List root-level folders and files |

### New Controller — `TrashController` (`/api/trash`)

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/api/trash` | List all soft-deleted items |
| `POST` | `/api/trash/{id:guid}/restore` | Restore a specific item |
| `DELETE` | `/api/trash` | Empty trash (permanently delete all) |

### Updated `FilesController`

| Endpoint | Change |
|---|---|
| `POST /api/files` | Accepts optional `folderId` query parameter |
| `DELETE /api/files/{id}` | Now soft-deletes (to Trash) instead of hard-deletes |
| `GET /api/files/{id}/versions` | NEW — list all versions |
| `POST /api/files/{id}/versions/{versionId}/restore` | NEW — restore a version |
| `DELETE /api/files/{id}/versions/{versionId}` | NEW — hard-delete a specific version |

### `ResultExtensions` — New Error Mappings

| Error | HTTP Status |
|---|---|
| `Folder.NotFound` | 404 |
| `Folder.Forbidden` | 403 |
| `Folder.MaxDepthExceeded` | 400 |
| `Folder.NameConflict` | 409 |
| `FileVersion.NotFound` | 404 |
| `FileVersion.CannotDeleteCurrent` | 400 |

---

## Implementation Order

```
1. DMS.Domain
   └── Folder entity + IFolderRepository
   └── FileVersion entity + IFileVersionRepository
   └── FileMetadata updated (FolderId, DeletedAt, SoftDelete/Restore/MoveTo)
   └── DomainErrors.Folder + DomainErrors.FileVersion

2. DMS.Application
   └── DTOs (FolderDto, FileVersionDto, FolderContentsDto, TrashedItemDto)
   └── Folder commands + queries
   └── Versioning commands + queries
   └── Trash commands + queries
   └── UploadFileCommand + handler updated (FolderId, versioning logic)
   └── DeleteFileCommand + handler updated (soft-delete)
   └── ITrashPurgeService interface
   └── DependencyInjection updated

3. DMS.Infrastructure
   └── FolderConfiguration + FileVersionConfiguration
   └── FileMetadataConfiguration updated
   └── FolderRepository (incl. recursive CTE)
   └── FileVersionRepository
   └── TrashPurgeBackgroundService
   └── DependencyInjection updated
   └── Migration: AddFoldersAndVersioning

4. DMS.Api
   └── ResultExtensions updated (new error codes)
   └── FoldersController
   └── TrashController
   └── FilesController updated (folderId param, soft-delete, versioning endpoints)
```
