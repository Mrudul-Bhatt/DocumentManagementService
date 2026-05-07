# Document Management Service

A production-quality Document Management Service built with **.NET 8** and **Clean Architecture**, implemented incrementally across levels — from a basic file store to a full AI-native intelligence layer. Each level introduces a realistic set of backend engineering challenges: authentication, folder trees, search indexing, real-time collaboration, and LLM-powered document understanding.

> See [`dms_requirements.txt`](dms_requirements.txt) for the full functional and non-functional requirements at every level.

---

## Architecture

The backend follows Clean Architecture with four layers. Dependency arrows always point inward — outer layers know about inner layers; inner layers know about nothing outside themselves.

```
┌──────────────────────────────────────────────────┐
│                    DMS.Api                        │
│   Controllers · Middleware · Swagger · Program    │
└───────────────────────┬──────────────────────────┘
                        │  Commands / Queries (MediatR)
┌───────────────────────▼──────────────────────────┐
│                 DMS.Application                   │
│   Handlers · Behaviours · DTOs · Service Interfaces │
└───────────────────────┬──────────────────────────┘
                        │  Repository / Service interfaces
┌───────────────────────▼──────────────────────────┐
│                  DMS.Domain                       │
│   Entities · Errors · Repository contracts        │
└──────────────────────────────────────────────────┘
                        ▲
┌───────────────────────┴──────────────────────────┐
│               DMS.Infrastructure                  │
│   EF Core · Repositories · Storage · Auth         │
└──────────────────────────────────────────────────┘
```

| Layer | Responsibility |
|---|---|
| **DMS.Domain** | Entities, domain rules, error catalogue, repository/service interfaces. Zero dependencies. |
| **DMS.Application** | Use-case orchestration via CQRS + MediatR. Defines service interfaces (`IPasswordHasher`, `IJwtTokenService`). Never references EF Core or HTTP. |
| **DMS.Infrastructure** | Implements every domain/application interface. Owns EF Core, BCrypt, JWT generation, file storage. |
| **DMS.Api** | HTTP surface. Controllers translate HTTP ↔ MediatR commands/queries. Owns middleware pipeline, Swagger, rate limiting. |

---

## Key Patterns

| Pattern | Where | Why |
|---|---|---|
| CQRS + MediatR | Application | Separates read/write paths; enables pipeline behaviours |
| Result pattern | Application / Domain | Typed error propagation without exceptions |
| Repository pattern | Domain interfaces / Infrastructure | Decouples persistence from business logic |
| Options pattern | Application (`JwtSettings`) | Strongly-typed config without `IConfiguration` outside Infrastructure |
| Pipeline behaviours (`IPipelineBehavior`) | Application | Cross-cutting concerns (audit logging) without touching handlers |
| Fluent API EF config | Infrastructure | Keeps domain entities free of EF annotations |
| RFC 7807 ProblemDetails | Api | Consistent, machine-readable error responses |

---

## Tech Stack

| Concern | Technology |
|---|---|
| Runtime | .NET 8 |
| API | ASP.NET Core |
| ORM | Entity Framework Core 8 |
| Database | SQL Server (Docker) |
| Mediator | MediatR |
| Authentication | JWT Bearer + BCrypt.Net-Next |
| Logging | Serilog |
| API docs | Swashbuckle (Swagger) |
| Frontend | React 18 + TypeScript + Vite |

---

## Level Progression

The project is built level by level. Each level is a separate Git branch that builds on the previous one.

### ✅ Level 0 — Flat File Storage
**Branch:** `Level-0`

MVP: store and retrieve files with no authentication.

- Upload, download, delete, list files
- Local disk storage; SQL Server for metadata
- Trusted `X-User-Id` header (no real auth)
- 25 MB file size cap
- Structured logging with Serilog

### ✅ Level 1 — Auth + Ownership Model
**Branch:** `Level-1`

Adds identity and ownership. Every endpoint is behind JWT authentication.

- User registration and login (email + BCrypt password)
- JWT access tokens (60 min) + opaque refresh tokens (7 days)
- Refresh token rotation — stolen tokens are detectable
- `[Authorize]` on all file endpoints; users can only access their own files
- Admin role via `Role` enum embedded in JWT claims
- Sliding window rate limiting on login (5 req/min per IP)
- Audit log: every file operation records who, what, when, and from where
- `AuditLoggingBehaviour` pipeline behaviour — transparent cross-cutting audit

### 🔜 Level 2 — Folders + Versioning
**Branch:** `Level-2` *(planned)*

Adds organisation and history.

- Create, rename, move, delete folders (arbitrary nesting, max 20 deep)
- Move files between folders
- Uploading a file with an existing name creates a new version (no overwrite)
- List, restore, and hard-delete specific file versions (90-day retention)
- Soft-delete to Trash; restore within 30 days; nightly auto-purge background service
- Atomic folder delete (transactional cascade to all descendants)

### 🔜 Level 3 — Sharing + Permissions
Viewer / Commenter / Editor roles per file or folder. Public shareable links with optional expiry. Redis permission cache (p99 < 20 ms). CDN for public links.

### 🔜 Level 4 — Full-Text Search + Metadata Indexing
Full-text search across file contents (PDF, DOCX, plain text). Near-real-time index (< 60 s post-upload). Tags, date range, file type filters. Content extraction via async worker queue. OCR for scanned documents.

### 🔜 Level 5 — Scalability + Multi-Tenancy
Organisation-level tenant isolation. Storage quotas, bandwidth tracking, per-tenant rate limits. SSO via SAML 2.0 / OIDC. Horizontal scaling with DB read replicas. 10,000 concurrent tenants.

### 🔜 Level 6 — Compliance + Security Hardening
AES-256 encryption at rest with customer-managed keys (AWS KMS / Azure Key Vault). Data residency per region. GDPR right-to-erasure. DLP scanning for PII. Legal hold. SOC 2 Type II audit artifacts.

### 🔜 Level 7 — Real-Time Collaboration
Presence indicators, inline comments, real-time co-editing (CRDT / OT). WebSocket sync (< 200 ms latency). Activity feed. @mention notifications.

### 🔜 Level N — AI-Native Intelligence Layer
Semantic search (natural language queries over document content). Document Q&A with citations. Auto-classification and summarisation on upload. Duplicate detection. PII redaction on download. RAG pipeline backed by a vector database (pgvector / Pinecone) and an LLM (Claude / GPT).

---

## Project Structure

```
DocumentManagementService/
├── src/
│   ├── DMS.Api/            # HTTP layer — controllers, middleware, Swagger
│   ├── DMS.Application/    # Use cases — CQRS handlers, behaviours, DTOs
│   ├── DMS.Domain/         # Core — entities, errors, repository interfaces
│   └── DMS.Infrastructure/ # Implementation — EF Core, auth services, file storage
├── frontend/               # React + TypeScript + Vite
├── docker-compose.yml      # SQL Server container
├── dms_requirements.txt    # Full requirements for all levels
└── readme.md               # This file
```

Each backend layer has its own README:
- [`src/DMS.Api/README.md`](src/DMS.Api/README.md)
- [`src/DMS.Application/README.md`](src/DMS.Application/README.md)
- [`src/DMS.Domain/README.md`](src/DMS.Domain/README.md)
- [`src/DMS.Infrastructure/README.md`](src/DMS.Infrastructure/README.md)

---

## API Endpoints (Level 1)

### Auth — `/api/auth`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | None | Create account; returns user + token pair |
| `POST` | `/api/auth/login` | None | Authenticate; returns JWT + refresh token |
| `POST` | `/api/auth/refresh` | None | Exchange refresh token for new token pair |
| `POST` | `/api/auth/revoke` | Bearer | Revoke a refresh token (explicit logout) |

### Files — `/api/files`

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/files` | Bearer | Upload a file (multipart/form-data, max 25 MB) |
| `GET` | `/api/files` | Bearer | List all files owned by the authenticated user |
| `GET` | `/api/files/{id:guid}` | Bearer | Download a file by its GUID |
| `DELETE` | `/api/files/{id:guid}` | Bearer | Delete a file by its GUID |

### System

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/health` | None | SQL Server connectivity health check |

---

## Running the Project

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Node.js 18+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- EF Core CLI: `dotnet tool install --global dotnet-ef`

### 1. Start the database

```bash
docker-compose up -d
```

Starts SQL Server 2022 Developer edition on port 1433. Wait ~15 seconds for the container to become healthy before proceeding.

### 2. Apply database migrations

```bash
dotnet ef database update --project src/DMS.Infrastructure --startup-project src/DMS.Api
```

Runs all pending migrations (currently: `InitialCreate`, `AddAuthAndAudit`).

### 3. Start the API

```bash
dotnet run --project src/DMS.Api
```

- API: `http://localhost:5035`
- Swagger UI: `http://localhost:5035/swagger`

Use the Swagger UI "Authorize" button to paste a JWT Bearer token after registering and logging in via `/api/auth/register` and `/api/auth/login`.

### 4. Start the frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend: `http://localhost:5173`

---

## Health Check

```
GET http://localhost:5035/health
```

Returns `200 Healthy` when SQL Server is reachable, `503 Unhealthy` otherwise.
