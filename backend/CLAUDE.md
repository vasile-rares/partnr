# Backend

.NET 10 API (Clean Architecture) with PostgreSQL. Paths and commands below are relative to `backend/`. General project rules (language, commit messages, working rules) are in the root `CLAUDE.md`.

**Reference for the structure:** [Clean Architecture in .NET 10](https://codewithmukesh.com/blog/clean-architecture-dotnet/) (codewithmukesh). The backend follows this article: four layers, plain services (no CQRS / MediatR), repositories + `IUnitOfWork`, thin controllers. When adding projects, folders or dependencies, consult it so the structure does not drift.

Solution: `ClientPortal.slnx`.

Folders are grouped **by role inside each layer** (not by feature); the namespace always matches the folder.

```
src/
  ClientPortal.Domain/           No NuGet packages.
    Entities/                    Entity (base), Client, Workspace, WorkspaceMember
    Enums/                       WorkspaceRole
    Exceptions/                  DomainException
  ClientPortal.Application/      Only the DI abstractions package.
    Interfaces/Repositories/     IClientRepository, IWorkspaceRepository, IUnitOfWork
    Interfaces/Services/         IAuthService, IClientService, IWorkspaceService, IIdentityService, ICurrentUser
    Services/                    AuthService, ClientService, WorkspaceService, WorkspaceAccess (all internal)
    DTOs/<Feature>/              Auth/, Clients/, Workspaces/ (requests and responses side by side)
    Mappings/                    ClientMappings, WorkspaceMappings (ToResponse() extension methods)
    Exceptions/                  AppException and its subclasses
    DependencyInjection.cs       AddApplication()
  ClientPortal.Infrastructure/   EF Core + Npgsql, ASP.NET Core Identity
    Persistence/                 ApplicationDbContext, Configurations/, Repositories/, Seeding/, Migrations/
    Authentication/              ApplicationUser, IdentityService, CurrentUser, cookie setup
    DependencyInjection.cs       AddInfrastructure()
  ClientPortal.Api/              Controllers/ (thin), Middleware/ (exception handler), Program.cs (composition root)
tests/
  ClientPortal.UnitTests/        Domain/, Application/ (services with fakes, request validation)
  ClientPortal.IntegrationTests/ Real Postgres via Testcontainers + WebApplicationFactory (needs Docker)
  ClientPortal.ArchitectureTests/ NetArchTest rules for the dependency rules below
```

Folders not created yet, to add when their first file appears: `Domain/ValueObjects/`, `Domain/Events/`, `Application/Validators/` (validation is data annotations on DTOs for now), `Infrastructure/Services/` (non-persistence implementations, e.g. email), `Api/Extensions/` (if `Program.cs` grows), `Api/Filters/`, `Api/DTOs/` (only for API-only shapes; use-case DTOs stay in Application). Do not create empty folders.

Integration tests live in a separate project so the fast unit tests can run on their own (`dotnet test tests/ClientPortal.UnitTests`). The architecture tests are fast too and need no database.

## Dependency rules

```
Api            -> Application, Infrastructure
Infrastructure -> Application
Application    -> Domain
Domain         -> nothing
```

- Dependencies point inward only. Domain depends on nothing external. These rules are checked by `tests/ClientPortal.ArchitectureTests` (`LayerDependencyTests`); add a test there when a new rule is agreed.
- Application talks to the database only through repository interfaces (`IClientRepository`, one per aggregate, in `Interfaces/Repositories/`) and `IUnitOfWork`. Both are implemented in Infrastructure: `ClientRepository` (reads, `Add`/`Remove`, never saves) and `ApplicationDbContext` (`IUnitOfWork.SaveChangesAsync`). Application has no EF Core reference.
- Api does not reference types from `Infrastructure.Persistence`; it only calls `AddInfrastructure()` from the composition root.

## Conventions

- **Services per feature**: `IXService` in `Application/Interfaces/Services/`, `XService` in `Application/Services/`, registered in `Application/DependencyInjection.cs`. Services are `internal`; only the interface and DTOs are public. A use case reads/changes entities through repositories and calls `unitOfWork.SaveChangesAsync` once at the end (one transaction per use case). Services are unit-tested with hand-written fake repositories.
- **Entities** have private setters and a static factory (`Client.Register`) that enforces invariants by throwing `DomainException`. Ids are UUID v7 (`Entity` base class).
- **Errors**: expected failures are exceptions mapped to HTTP by `Api/Middleware/ApiExceptionHandler` as `ProblemDetails`. Services throw `NotFoundException` (404) / `ConflictException` (409), both `AppException`s from `Application/Exceptions/` (add a new subclass for another status code). Entities throw `DomainException` (400, `Domain/Exceptions/`) for broken invariants. Anything else becomes 500 via `UseExceptionHandler()`. Controllers stay free of error handling.
- **Input validation** with data annotations (`[Required]`, `[MaxLength]`, `[EmailAddress]`) on the request DTOs in `Application/DTOs/<Feature>/` (`XRequest` and `XResponse` side by side), validated automatically by `[ApiController]` (returns 400 `ValidationProblemDetails`). Use non-positional records with `init` properties so the attributes sit on properties. Data annotations go on DTOs only, never on entities.
- **Mapping** by hand with extension methods in `Application/Mappings/` (`ClientMappings.ToResponse()`), no AutoMapper.
- **Unique-constraint violations** (Postgres `23505`) are translated to `ConflictException` inside `ApplicationDbContext.SaveChangesAsync`, so services need no try/catch for races.
- **EF mapping** with Fluent API in `Infrastructure/Persistence/Configurations/` (`IEntityTypeConfiguration<T>`), never data annotations on entities.
- Naming: plural folders for roles (`Entities/`, `Services/`, `Controllers/`), plural feature names under `DTOs/` (`DTOs/Clients/`); suffix files with their role (`ClientService`, `ClientRepository`, `ClientsController`, `ClientConfiguration`).
- Shared settings live in `Directory.Build.props` (`net10.0`, nullable, `TreatWarningsAsErrors=true`). Do not repeat them in `.csproj` files.
- **NuGet package versions only in `Directory.Packages.props`** (central package management). In `.csproj`: `<PackageReference Include="X" />`, no `Version`. The `Microsoft.OpenApi` pin in that file addresses a vulnerability (GHSA-v5pm-xwqc-g5wc) — do not remove it without checking.

## Authentication

- **ASP.NET Core Identity** with a **cookie** (HttpOnly, SameSite=Lax, 14 days sliding), no JWT. `AuthService` (the use cases: duplicate-email check, generic login error, mapping) is in `Application/Services/` and is unit-tested with a fake. It talks to Identity only through the `IIdentityService` port, implemented by a thin adapter in `Infrastructure/Authentication/` (`IdentityService`, plus `ApplicationUser`) on top of `UserManager`/`SignInManager`. It is a port, not a repository: Identity saves by itself and also owns passwords, lockout and the cookie. `ICurrentUser` is implemented by `Infrastructure/Authentication/CurrentUser` (reads the user id claim from `HttpContext`), registered in `AddInfrastructure()`.
- Endpoints: `POST /api/auth/register|login|logout`, `GET /api/auth/me`. Login failures are always the same 401 message (wrong password, unknown email and lockout are indistinguishable). 5 wrong passwords lock the account for 15 minutes.
- **The whole API requires login by default** (fallback policy in `Program.cs`). A public endpoint must be marked `[AllowAnonymous]`; a new controller needs no attribute to be protected.
- Identity's cookie handler would redirect to a login page; it is overridden to answer 401 / 403 (an API has no login page).
- CORS must keep `AllowCredentials()` or the browser will not send the cookie from `localhost:3000`.
- Roles are not global. They will belong to workspace memberships (next step), so `ApplicationDbContext` extends `IdentityUserContext` (no role tables).
- Email confirmation and password reset are not built yet; they come with the email service (Invitations step).

## Authorization (workspaces)

- Everything a freelancer or agency owns lives in a **workspace**, and its routes say so: `/api/workspaces/{workspaceId}/clients/...`. Roles (`Owner`, `Member`) belong to the membership (`WorkspaceMember`), never to the account.
- **Every service that touches data inside a workspace calls `WorkspaceAccess` first** (`Application/Services/WorkspaceAccess.cs`): `RequireMemberAsync` or `RequireOwnerAsync`. Do not re-implement the check in a service or a controller.
- A non-member gets **404**, exactly like a missing workspace, so ids cannot be probed. A member doing an owner-only action gets **403** (`ForbiddenException`), since they already know the workspace exists.
- Repositories of workspace data take the `workspaceId` in every lookup (`GetByIdAsync(workspaceId, id)`), so a record of another workspace is not found even by its id.
- Clients are not members: their portal access will come from invitations, scoped to their own projects.
- Clients: any member adds and edits them and sees all of them; **archiving and restoring is owner-only** (`RequireOwnerAsync`).
- Members cannot be added through the API yet (invitations step); integration tests add them straight to the database.

## Adding a new feature (e.g. `Projects`)

Use `Clients` as the template. Every step is needed; nothing fails at build time if the DI lines are forgotten, the app only breaks on the first request.

1. **Domain**: `Domain/Entities/Project.cs` (private setters, static factory, throws `DomainException`); enums in `Domain/Enums/`.
2. **Application**: `Interfaces/Services/IProjectService.cs`, `Services/ProjectService.cs` (`internal`, calls `WorkspaceAccess` first if the data belongs to a workspace), `Interfaces/Repositories/IProjectRepository.cs`, `Mappings/ProjectMappings.cs`, `DTOs/Projects/` (requests and responses). One repository per aggregate, not per entity.
3. **Application DI**: register `IProjectService` in `Application/DependencyInjection.cs`.
4. **Infrastructure**: `DbSet<Project>` in `ApplicationDbContext`, `Persistence/Configurations/ProjectConfiguration.cs`, `Persistence/Repositories/ProjectRepository.cs`, and register `IProjectRepository` in `Infrastructure/DependencyInjection.cs`.
5. **Migration**: `dotnet ef migrations add <Name> ...` (see Database and migrations), then review the generated file.
6. **Api**: `Controllers/ProjectsController.cs`, thin, no error handling.
7. **Tests**: service tests in `tests/ClientPortal.UnitTests/Application/` (fake repository + fake unit of work), entity tests in `Domain/`.
8. Run `dotnet build` (0 warnings) and `dotnet test`.

External services (email, OAuth, AI, file storage) follow the same rule as repositories: the interface lives in `Application/Interfaces/Services/`, the implementation in `Infrastructure/Services/`, never in `Persistence/`.

## Database and migrations

- PostgreSQL 17 via `docker-compose.yml` (db `client_portal`, local-dev-only credentials `postgres`/`postgres`, port 5432). The `Postgres` connection string is in `src/ClientPortal.Api/appsettings.Development.json`. To use a different Postgres (e.g. one installed locally), override it with user secrets instead of editing that file: `dotnet user-secrets set "ConnectionStrings:Postgres" "Host=...;Password=..." --project src/ClientPortal.Api`.
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
dotnet run --project src/ClientPortal.Api --launch-profile http -- --seed   # test data, then exits
```

- **Test data (development only):** `-- --seed` (VS Code task `db-seed`) applies pending migrations and adds demo accounts, workspaces and clients, then exits. It is never automatic, so the e2e and integration databases stay empty. It is safe to run again: when `demo@example.com` exists it does nothing. Sign in as `demo@example.com` (owner of "Studio Pop" and "Side Project") or `member@example.com` (a plain member of "Studio Pop"), password `demo-password`. The data lives in `Infrastructure/Persistence/Seeding/DevelopmentDataSeeder.cs`; add to it when a new feature needs something to click through. Stop the running API first (a debugger locks the DLLs `dotnet run` has to rebuild).

- API docs (Development only): Scalar at `http://localhost:5051/scalar/v1`, spec at `/openapi/v1.json`.
- CORS allows `http://localhost:3000` (frontend).
- Before saying a change is done: `dotnet build` with zero warnings and a green `dotnet test`.

## Prerequisites

- .NET SDK 10 (version pinned in `global.json`), the `dotnet-ef` tool (`dotnet tool install --global dotnet-ef`), Docker (for Postgres).
- If `docker compose up` fails with a network timeout while pulling images, retry before reporting a failure.
