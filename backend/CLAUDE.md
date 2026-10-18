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
    Services/                    One use-case service per feature, nothing else (all internal, names end in "Service", enforced by an architecture test)
    Authorization/               WorkspaceAccess (the one place that decides who may touch a workspace)
    Common/                      TextHelpers (small helpers shared by services)
    DTOs/<Feature>/              Auth/, Clients/, Workspaces/ (requests and responses side by side)
    Mappings/                    WorkspaceMappings (ToResponse() extension methods), ClientResponseBuilder (clients need the account and invitation data too, so it is a class, not an extension)
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
- **Mapping** by hand with extension methods in `Application/Mappings/` (`WorkspaceMappings.ToResponse()`), no AutoMapper.
- **Unique-constraint violations** (Postgres `23505`) are translated to `ConflictException` inside `ApplicationDbContext.SaveChangesAsync`, so services need no try/catch for races.
- **EF mapping** with Fluent API in `Infrastructure/Persistence/Configurations/` (`IEntityTypeConfiguration<T>`), never data annotations on entities.
- Naming: plural folders for roles (`Entities/`, `Services/`, `Controllers/`), plural feature names under `DTOs/` (`DTOs/Clients/`); suffix files with their role (`ClientService`, `ClientRepository`, `ClientsController`, `ClientConfiguration`).
- Shared settings live in `Directory.Build.props` (`net10.0`, nullable, `TreatWarningsAsErrors=true`). Do not repeat them in `.csproj` files.
- **NuGet package versions only in `Directory.Packages.props`** (central package management). In `.csproj`: `<PackageReference Include="X" />`, no `Version`. The `Microsoft.OpenApi` pin in that file addresses a vulnerability (GHSA-v5pm-xwqc-g5wc) — do not remove it without checking.

## Authentication

- **ASP.NET Core Identity** with a **cookie** (HttpOnly, SameSite=Lax, 14 days sliding), no JWT. `AuthService` (the use cases: duplicate-email check, generic login error, mapping) is in `Application/Services/` and is unit-tested with a fake. It talks to Identity only through the `IIdentityService` port, implemented by a thin adapter in `Infrastructure/Authentication/` (`IdentityService`, plus `ApplicationUser`) on top of `UserManager`/`SignInManager`. It is a port, not a repository: Identity saves by itself and also owns passwords, lockout and the cookie. `ICurrentUser` is implemented by `Infrastructure/Authentication/CurrentUser` (reads the user id claim from `HttpContext`), registered in `AddInfrastructure()`.
- Endpoints: `POST /api/auth/register|login|logout`, `GET /api/auth/me`. Login failures are always the same 401 message (wrong password, unknown email and lockout are indistinguishable). 5 wrong passwords lock the account for 15 minutes.
- **The whole API requires login by default** (fallback policy in `Program.cs`). A public endpoint must be marked `[AllowAnonymous]`; a new controller needs no attribute to be protected.
- **The account behind the cookie is checked on every request** (`SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero`; Identity's default is 30 minutes). A cookie whose account is gone (deleted, or the development database was reseeded) is then plain anonymous: a clean 401 and the cookie is cleared. Without it such a cookie counted as signed in, so some endpoints answered 200 with empty data and `/api/auth/me` threw `UnauthorizedException` from inside the service. The cost is one primary-key lookup per request.
- Identity's cookie handler would redirect to a login page; it is overridden to answer 401 / 403 (an API has no login page).
- CORS must keep `AllowCredentials()` or the browser will not send the cookie from `localhost:3000`.
- Roles are not global. They will belong to workspace memberships (next step), so `ApplicationDbContext` extends `IdentityUserContext` (no role tables).
- Email confirmation and password reset are not built yet; they come with the email service (Invitations step).

## Authorization (workspaces)

- Everything a freelancer or agency owns lives in a **workspace**, and its routes say so: `/api/workspaces/{workspaceId}/clients/...`. Roles (`Owner`, `Member`) belong to the membership (`WorkspaceMember`), never to the account.
- **Every service that touches data inside a workspace calls `WorkspaceAccess` first** (`Application/Authorization/WorkspaceAccess.cs`): `RequireMemberAsync` or `RequireOwnerAsync`. Do not re-implement the check in a service or a controller.
- A non-member gets **404**, exactly like a missing workspace, so ids cannot be probed. A member doing an owner-only action gets **403** (`ForbiddenException`), since they already know the workspace exists.
- Repositories of workspace data take the `workspaceId` in every lookup (`GetByIdAsync(workspaceId, id)`), so a record of another workspace is not found even by its id.
- Clients are not members: their portal access comes from invitations (`/api/invitations/{token}`, public to the holder of the link, except `accept` which needs the invited account) and is read through `/api/portal/...`, which is about the signed-in account itself and so has no workspace in the route. A client never passes `WorkspaceAccess`: an account whose card is linked sees its workspaces' names in `GET /api/portal/workspaces` and its shared projects in `GET /api/portal/projects[/{id}]` (archiving the card hides both). A project is shared when `IProjectRepository.SharedWithClient` says so: the account's card is linked and not archived, the project is not archived and has left `Planning`. A draft, an archived project, another client's project and an unknown id all answer the same 404. The portal has its own DTOs (`DTOs/Portal/`), so nothing of the workspace's side (client card, notes, ids of cards) can reach a client by adding a field to `ProjectResponse`.
- The account profile (`PUT /api/auth/me`: full name, phone, company) is the user's own; the email is not part of it and cannot be changed yet. The phone is Identity's `PhoneNumber` column.
- Clients: any member adds and edits them and sees all of them; **archiving and restoring is owner-only** (`RequireOwnerAsync`).
- Projects (`/api/workspaces/{id}/projects`, `?includeArchived=true&clientId=`) follow the same rules: members create and edit, the owner archives and restores. A project belongs to one client of the same workspace, chosen at creation and never changed (the update request has no `clientId`); an archived client cannot get a new one (409). An **archived project is read-only**: every change answers 409 until it is restored. The status (`Planning`, `Active`, `OnHold`, `Completed`) is set by hand and stored as text; `Planning` means the client will not see the project in the portal. Stages are part of the `Project` aggregate (`AddStage`/`UpdateStage`/`MoveStage`/`RemoveStage`, positions always 0, 1, 2... with no unique index on them, see the plan) and are changed through `.../projects/{id}/stages[/{stageId}[/move]]`; each call answers with the whole project, and an archived project refuses them (409). `ProjectResponseBuilder` names the client through `ClientResponseBuilder`, so a client with an account shows the account's name.
- Deliverables (`/api/workspaces/{id}/projects/{projectId}/deliverables`, `?includeArchived=true`) are their own aggregate (`Deliverable`, `IDeliverableRepository`), tied to a project and optionally to one of its stages (deleting the stage only clears the link, `ON DELETE SET NULL`). The link must be `https` (checked in the entity). `Draft` / `Shared` is set by hand; the client sees only `Shared`, non-archived ones, inside `GET /api/portal/projects/{id}` (`deliverables`, `PortalDeliverableResponse`; `IDeliverableRepository.ListSharedAsync` runs only after the project itself was found as shared with the client, so a draft or archived project hides its deliverables too). Members create, edit, share and delete a draft (a shared one answers 409); **archiving and restoring is owner-only**, archiving also sets the status back to `Draft`. An archived deliverable and every deliverable of an archived project are read-only (409). A stage or deliverable of another project is a 404.
- Approvals are rounds (`ApprovalRequest`, `IApprovalRequestRepository`, never removed): a member asks for one on a `Shared` deliverable (`POST .../deliverables/{id}/approvals` with an optional `note`, 409 on a draft, an archived deliverable or while one is `Pending`; `.../approvals/{approvalId}/withdraw` takes it back), and a partial unique index allows only one `Pending` round per deliverable. `DeliverableResponse` carries `approvalStatus` (`None`, `Pending`, `Approved`, `ChangesRequested`: the latest round that was not withdrawn) and every round newest first in `approvals`, built by `DeliverableResponseBuilder`. An open round **freezes the deliverable's details** (update answers 409); `unshare` and `archive` **withdraw** it; a deliverable with any round in its history **cannot be deleted** (409, archive it). The client answers through `POST /api/portal/projects/{projectId}/deliverables/{deliverableId}/approvals/{approvalId}/approve|request-changes` (`PortalApprovalService`; body `message`, optional on approve, **required** on request-changes; the answer is the updated `PortalDeliverableResponse`). It goes through the same door as reading the project (`GetSharedWithClientAsync`), so a draft or archived project, someone else's project, an unshared or archived deliverable and an unknown round are all 404, and a round that is no longer open (answered or withdrawn) is a 409; a decision is final. The portal's `PortalDeliverableResponse` carries `approval` (`status`, the open round as `pending` with its note, and the decided rounds as `history` newest first; **withdrawn rounds never show**, and no ids or accounts), and projects carry `awaitingApproval` (the list and the single project), counting only pending rounds on deliverables the client can see (`IApprovalRequestRepository.CountPendingForClientAsync`).
- Project updates, the timeline's written notes (`/api/workspaces/{id}/projects/{projectId}/updates`, `?includeArchived=true`), are their own aggregate (`ProjectUpdate`, `IProjectUpdateRepository`) with a `Title` and an optional plain-text `Body`, and follow the deliverables' rules exactly: `Draft` / `Shared` by hand, members create, edit, share and delete a draft (a shared one answers 409), archive and restore are owner-only and archiving sets the status back to `Draft`, an archived update and every update of an archived project are read-only (409) but can be read, another project's update is a 404. `SharedAtUtc` is set on the first share and kept; the list the freelancer reads is newest created first. The client sees only `Shared`, non-archived ones inside `GET /api/portal/projects/{id}` (`updates`, `PortalProjectUpdateResponse`: id, title, body and the share date only, most recently shared first; `IProjectUpdateRepository.ListSharedAsync` runs only after the project itself was found as shared with the client). There is no automatic event log; see the plan's ideas.
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
- **Email** goes through `IEmailSender` (Application), implemented by `Infrastructure/Email/SmtpEmailSender` (MailKit) and configured by the `Email` section (`From`, `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword`, `UseTls`), validated at startup. In development the SMTP server is **Mailpit** from `docker-compose.yml` (SMTP on 1025, inbox UI on http://localhost:8025): every email is caught there, none reaches a real inbox. Production only changes the configuration. Integration tests replace the sender with `FakeEmailSender` (`ApiFactory.Emails`); `Email/SmtpEmailSenderTests` checks the real sender against a Mailpit container.
- Migrations are created in Infrastructure, started from Api (commands work in any shell):

```bash
dotnet ef migrations add <Name> --project src/ClientPortal.Infrastructure --startup-project src/ClientPortal.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/ClientPortal.Infrastructure --startup-project src/ClientPortal.Api
```

## Common commands

```bash
docker compose up -d                                              # Postgres + Mailpit (http://localhost:8025)
dotnet build                                                      # must be 0 warnings
dotnet test
dotnet run --project src/ClientPortal.Api --launch-profile http   # http://localhost:5051
dotnet run --project src/ClientPortal.Api --launch-profile http -- --seed   # test data, then exits
```

- **Client name is optional** (`Client.Name`, `ClientResponse.Name` are nullable): a card can be just an email. UIs show `name ?? email` until the client has an account, whose name replaces it.
- **Account suggestions** (`GET /api/workspaces/{id}/clients/suggestions?email=`, `ClientSuggestionService`) are a directory of accounts, so they are narrow on purpose: members of the workspace only (404 otherwise), at least 3 characters (an empty list below that), at most 5 results, a match on the start of the email only (never the name), workspace members left out. The endpoint has its own **rate limit** (`RateLimitPolicies.Suggestions`, 60 a minute per signed-in user, answering 429), defined in `Program.cs` with `AddRateLimiter`/`UseRateLimiter` (placed after authentication so it can follow the user). New endpoints that look people up should reuse that policy or add one next to it.
- **Test data (development only):** `-- --seed` (VS Code task `db-seed`) applies pending migrations and adds demo accounts, workspaces and clients, then exits. It is never automatic, so the e2e and integration databases stay empty. It is safe to run again: when `demo@example.com` exists it does nothing. **F5 ("API" launch profile) reseeds on its own when the seed changed**: it sets `Seed__ReseedOnChange=true`, and at startup the API compares a fingerprint of the seed's source (`SeedFingerprint`: `DevelopmentSeed.cs` and `DevelopmentDataSeeder.cs`, embedded in the assembly at build) with the one stored as the database's own comment (`COMMENT ON DATABASE`, no table). If they differ, or the database has demo data but no fingerprint, **every table is dropped (`DROP SCHEMA public CASCADE`), recreated by the migrations and seeded again, so anything entered by hand is lost**; it refuses a database that is not on localhost. It is off by default because the integration and e2e runs are in Development too; `dotnet run` without that variable never recreates anything (use `-- --seed` by hand). Sign in as `demo@example.com` (owner of "Studio Pop" and "Side Project"), `member@example.com` (a plain member of "Studio Pop") or `client@example.com` (a client of "Studio Pop" who accepted an invitation), password `demo-password`. Elena's card has two projects: "Elena Design website" (Active, with four stages: two Done, one In progress, one To do) and "Brand refresh" (Planning, so hidden from her portal). The website has two deliverables: "Homepage mockups" (Shared, on the Design stage) and "Staging site" (Draft), and two updates: "Design approved" (Shared) and "Staging link coming soon" (Draft). "Homepage mockups" has an old approval round where Elena asked for changes ("The logo is too small on mobile.") and a new one still waiting for her, so her portal shows "1 waiting for you". "Pat Pending" in "Studio Pop" has an invitation waiting, openable at `/invite/dev-pending-invitation-token` without any email. The data lives in `Infrastructure/Persistence/Seeding/DevelopmentDataSeeder.cs`; add to it when a new feature needs something to click through. Stop the running API first (a debugger locks the DLLs `dotnet run` has to rebuild).

- API docs (Development only): Scalar at `http://localhost:5051/scalar/v1`, spec at `/openapi/v1.json`.
- CORS allows `http://localhost:3000` (frontend).
- Before saying a change is done: `dotnet build` with zero warnings and a green `dotnet test`.

## Prerequisites

- .NET SDK 10 (version pinned in `global.json`), the `dotnet-ef` tool (`dotnet tool install --global dotnet-ef`), Docker (for Postgres).
- If `docker compose up` fails with a network timeout while pulling images, retry before reporting a failure.
