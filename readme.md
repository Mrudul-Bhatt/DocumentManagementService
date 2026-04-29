# Document Management Service

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Node.js 18+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [EF Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet): `dotnet tool install --global dotnet-ef`

---

## Running the Project

### 1. Start the database

```bash
docker-compose up -d
```

### 2. Apply database migrations

```bash
dotnet ef database update --project src/DMS.Infrastructure --startup-project src/DMS.Api
```

### 3. Start the API

```bash
dotnet run --project src/DMS.Api
```

API is available at `http://localhost:5035`  
Swagger UI at `http://localhost:5035/swagger`

### 4. Start the frontend

In a separate terminal:

```bash
cd frontend
npm install
npm run dev
```

Frontend is available at `http://localhost:5173`

---

## Health Check

```
GET http://localhost:5035/health
```
