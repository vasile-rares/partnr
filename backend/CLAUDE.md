# Backend

.NET 10 API (Clean Architecture) with PostgreSQL. Paths and commands below are relative to `backend/`. General project rules (language, commit messages, working rules) are in the root `CLAUDE.md`.

**Reference for the structure:** [Clean Architecture in .NET 10](https://codewithmukesh.com/blog/clean-architecture-dotnet/) (codewithmukesh). The backend follows this article: four layers, plain services (no CQRS / MediatR), repositories + `IUnitOfWork`, thin controllers. When adding projects, folders or dependencies, consult it so the structure does not drift.

Solution: `ClientPortal.slnx`.

```
src/
  ClientPortal.Domain/           Common/ (Entity, DomainException), one folder per aggregate (Clients/Client.cs). No NuGet packages.
  ClientPortal.Application/      Common/ (IUnitOfWork, Exceptions/), one folder per feature (Clients/: IClientService, ClientService, IClientRepository, ClientMappings, Dtos/Requests|Responses/). Only the DI abstractions package.
  ClientPortal.Infrastructure/   EF Core + Npgsql: Persistence/ (ApplicationDbContext, Configurations/, Repositories/, Migrations/)
  ClientPortal.Api/              Controllers/ (thin), Middleware/ (exception handler), Program.cs (composition root)
tests/
  ClientPortal.Tests/            Unit tests (Domain/, Application/)
```

Tests stay in a single project with folders mirroring the layers. Integration tests (real Postgres, `WebApplicationFactory`) go in a separate project, `tests/ClientPortal.IntegrationTests/`, so the fast unit tests can run on their own.

## Dependency rules

```
Api            -> Application, Infrastructure
Infrastructure -> Application
Application    -> Domain
Domain         -> nothing
```

- Dependencies point inward only. Domain depends on nothing external.
- Application talks to the database only through repository interfaces (`IClientRepository`, one per aggregate, declared next to the feature) and `IUnitOfWork`. Both are implemented in Infrastructure: `ClientRepository` (reads, `Add`/`Remove`, never saves) and `ApplicationDbContext` (`IUnitOfWork.SaveChangesAsync`). Application has no EF Core reference.
- Api does not reference types from `Infrastructure.Persistence`; it only calls `AddInfrastructure()` from the composition root.

## Conventions

- **Services per feature**: `IXService` / `XService` in `Application/<Feature>/`, registered in `Application/DependencyInjection.cs`. Services are `internal`; only the interface and DTOs are public. A use case reads/changes entities through repositories and calls `unitOfWork.SaveChangesAsync` once at the end (one transaction per use case). Services are unit-tested with hand-written fake repositories.
- **Entities** have private setters and a static factory (`Client.Register`) that enforces invariants by throwing `DomainException`. Ids are UUID v7 (`Entity` base class).
- **Errors**: expected failures are exceptions mapped to HTTP by `Api/Middleware/ApiExceptionHandler` as `ProblemDetails`. Services throw `NotFoundException` (404) / `ConflictException` (409), both `AppException`s from `Application/Common/Exceptions/` (add a new subclass for another status code). Entities throw `DomainException` (400) for broken invariants. Anything else becomes 500 via `UseExceptionHandler()`. Controllers stay free of error handling.
- **Input validation** with data annotations (`[Required]`, `[MaxLength]`, `[EmailAddress]`) on the request DTOs in `Application/<Feature>/Dtos/Requests/` (responses go in `Dtos/Responses/`), validated automatically by `[ApiController]` (returns 400 `ValidationProblemDetails`). Use non-positional records with `init` properties so the attributes sit on properties. Data annotations go on DTOs only, never on entities.
- **Mapping** by hand with extension methods (`ClientMappings.ToResponse()`), no AutoMapper.
- **Unique-constraint violations** (Postgres `23505`) are translated to `ConflictException` inside `ApplicationDbContext.SaveChangesAsync`, so services need no try/catch for races.
- **EF mapping** with Fluent API in `Infrastructure/Persistence/Configurations/` (`IEntityTypeConfiguration<T>`), never data annotations on entities.
- Naming: plural folders for collections (`Dtos/`, `Requests/`, `Responses/`, `Controllers/`), singular for feature areas (`Clients/`); suffix files with their role (`ClientService`, `ClientRepository`, `ClientsController`, `ClientConfiguration`).
- Shared settings live in `Directory.Build.props` (`net10.0`, nullable, `TreatWarningsAsErrors=true`). Do not repeat them in `.csproj` files.
- **NuGet package versions only in `Directory.Packages.props`** (central package management). In `.csproj`: `<PackageReference Include="X" />`, no `Version`. The `Microsoft.OpenApi` pin in that file addresses a vulnerability (GHSA-v5pm-xwqc-g5wc) — do not remove it without checking.

## Adding a new feature (e.g. `Projects`)

Use `Clients` as the template. Every step is needed; nothing fails at build time if the DI lines are forgotten, the app only breaks on the first request.

1. **Domain**: `Domain/Projects/Project.cs` (private setters, static factory, throws `DomainException`).
2. **Application** (`Application/Projects/`): `IProjectService`, `ProjectService` (`internal`), `IProjectRepository`, `ProjectMappings`, `Dtos/Requests/`, `Dtos/Responses/`. One repository per aggregate; keep closely related entities in the same feature.
3. **Application DI**: register `IProjectService` in `Application/DependencyInjection.cs`.
4. **Infrastructure**: `DbSet<Project>` in `ApplicationDbContext`, `Persistence/Configurations/ProjectConfiguration.cs`, `Persistence/Repositories/ProjectRepository.cs`, and register `IProjectRepository` in `Infrastructure/DependencyInjection.cs`.
5. **Migration**: `dotnet ef migrations add <Name> ...` (see Database and migrations), then review the generated file.
6. **Api**: `Controllers/ProjectsController.cs`, thin, no error handling.
7. **Tests**: service tests in `tests/ClientPortal.Tests/Application/` (fake repository + fake unit of work), entity tests in `Domain/`.
8. Run `dotnet build` (0 warnings) and `dotnet test`.

External services (email, OAuth, AI, file storage) follow the same rule as repositories: the interface lives in Application, the implementation in `Infrastructure/External/<Name>/`, never in `Persistence/`.

## Database and migrations

- PostgreSQL 17 via `docker-compose.yml` (db `client_portal`, local-dev-only credentials `postgres`/`postgres`, port 5432). The `Postgres` connection string is in `src/ClientPortal.Api/appsettings.Development.json`.
- Migrations are created in Infrastructure, started from Api (commands work in any shell):

```bash
dotnet ef migrations add <Name> --project src/ClientPortal.Infrastructure --startup-project src/ClientPortal.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/ClientPortal.Infrastructure --startup-project src/ClientPortal.Api
```

## Common commands

```bash
docker compose up -d                                              # Postgres
dotnet build                                                      # must be 0 warnings
dotnet test
dotnet run --project src/ClientPortal.Api --launch-profile http   # http://localhost:5051
```

- API docs (Development only): Scalar at `http://localhost:5051/scalar/v1`, spec at `/openapi/v1.json`.
- CORS allows `http://localhost:3000` (frontend).
- Before saying a change is done: `dotnet build` with zero warnings and a green `dotnet test`.

## Prerequisites

- .NET SDK 10 (version pinned in `global.json`), the `dotnet-ef` tool (`dotnet tool install --global dotnet-ef`), Docker (for Postgres).
- If `docker compose up` fails with a network timeout while pulling images, retry before reporting a failure.
