# Backend

.NET 10 API (Clean Architecture) with PostgreSQL. Paths and commands below are relative to `backend/`. General project rules (language, commit messages, working rules) are in the root `CLAUDE.md`.

**Reference for the structure:** [How to design a maintainable .NET solution structure for growing teams](https://dev.to/mashrulhaque/how-to-design-a-maintainable-net-solution-structure-for-growing-teams-284n). The backend follows this article (Clean Architecture, feature folders, central build config, architecture tests). When adding projects, folders or dependencies, consult it so the structure does not drift.

Solution: `ClientPortal.slnx`.

```
src/
  ClientPortal.SharedKernel/     Entity, DomainEvent, Result, Error (primitives only, keep it small)
  ClientPortal.Domain/           Entities, events, errors, repository interfaces. No NuGet packages.
  ClientPortal.Application/      Use cases (Features/), validators, abstractions
  ClientPortal.Infrastructure/   EF Core + Npgsql, repositories, external services
  ClientPortal.Api/              Thin controllers, Program.cs (composition root)
tests/
  ClientPortal.{Domain,Application,Infrastructure,Api}.Tests/
  ClientPortal.Architecture.Tests/   NetArchTest — enforces the dependency rules
```

## Dependency rules (enforced by tests)

```
Api            -> Application, Infrastructure
Infrastructure -> Application
Application    -> Domain
Domain         -> SharedKernel
```

- Dependencies point inward only. Domain depends on nothing external.
- **Interfaces live with the code that uses them**; implementations live in Infrastructure (e.g. `IClientRepository` in Domain, `ClientRepository` in Infrastructure).
- Api does not reference types from `Infrastructure.Persistence`; it only calls `AddInfrastructure()` from the composition root.
- When adding a new layer or dependency, update `DependencyRulesTests`.

## Conventions

- **Feature folders per use case**: `Application/Features/<Feature>/Commands|Queries/<UseCase>/` containing `Command|Query`, `Handler`, `Validator`, DTO. No "XService" classes that do everything.
- **No MediatR** (commercial license from v13). Handlers implement `ICommandHandler<,>` / `IQueryHandler<,>` and are registered automatically in `Application/DependencyInjection.cs`.
- **Expected errors = `Result<T>` / `Error`**, not exceptions. Domain errors live in `Domain/Errors/`. Controllers map `Result` to HTTP (`ProblemDetails`) via `Api/Extensions/ResultExtensions.cs`.
- **Validation** with FluentValidation, in Application.
- Naming: plural folders for collections (`Entities/`), singular for feature areas (`Clients/`); suffix files with their role (`ClientRepository`, `RegisterClientHandler`).
- Shared settings live in `Directory.Build.props` (`net10.0`, nullable, `TreatWarningsAsErrors=true`). Do not repeat them in `.csproj` files.
- **NuGet package versions only in `Directory.Packages.props`** (central package management). In `.csproj`: `<PackageReference Include="X" />`, no `Version`. The `Microsoft.OpenApi` pin in that file addresses a vulnerability (GHSA-v5pm-xwqc-g5wc) — do not remove it without checking.

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
