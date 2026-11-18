# Backend

.NET 10 API (Clean Architecture) with PostgreSQL. Paths and commands below are relative to `backend/`. General project rules (language, commit messages, working rules) are in the root `CLAUDE.md`.

**Reference for the structure:** [Clean Architecture in .NET 10](https://codewithmukesh.com/blog/clean-architecture-dotnet/) (codewithmukesh). The backend follows this article: four layers, plain services (no CQRS / MediatR), repositories + `IUnitOfWork`, thin controllers. When adding projects, folders or dependencies, consult it so the structure does not drift.

Solution: `Partnr.slnx`.

The backend is being split into services (plan: `.claude/docs/microservices-plan.md`). Until the other services are extracted, everything below is the **Workspaces** service, which holds the whole monolith: its projects live in `src/Services/Workspaces/` and its tests in `tests/Workspaces/`, all named `Partnr.Workspaces.*` (namespace included). Each service that comes later gets the same four layers in `src/Services/<Name>/`.

Folders are grouped **by role inside each layer** (not by feature); the namespace always matches the folder.

```
src/Services/Workspaces/
  Partnr.Workspaces.Domain/           No NuGet packages.
    Entities/                    Entity (base, UUID v7 ids) and one class per entity; aggregates own their children (Project owns its stages)
    Enums/                       Statuses and roles, stored as text
    Exceptions/                  DomainException
    Common/                      SecureToken (random tokens, only their hash is stored)
  Partnr.Workspaces.Application/      Only the DI abstractions package.
    Interfaces/Repositories/     One repository per aggregate, plus IUnitOfWork
    Interfaces/Services/         The use-case services (IXService) and the ports Infrastructure implements (IEmailSender, IFileStorage, IGoogleCalendarClient, ICurrentUser, ...)
    Services/                    One use-case service per feature, nothing else (all internal, names end in "Service", enforced by an architecture test)
    Authorization/               WorkspaceAccess (who may touch a workspace), ClientAccess (what a client's account may open in the portal), CallerAccess (member or client, for routes open to both), ProjectAccess (finds a project and refuses changes once archived), ProjectContentRules (what deliverables, updates and files share)
    Messaging/                   ConversationThreads (the conversation rules `MessageService` applies to members and clients alike)
    Notifications/               NotificationWriter (the INotificationWriter port: services call it before SaveChanges, it only adds rows), NotificationContent (subject, text and link of a notification)
    Common/                      Small helpers shared by services (TextHelpers, AppInfo)
    DTOs/<Feature>/              Requests and responses side by side; the client side has its own (DTOs/ClientView/)
    Mappings/                    ToResponse() extension methods; a *ResponseBuilder class when a response needs more data than the entity (a repository or the account)
    Exceptions/                  AppException and its subclasses
    DependencyInjection.cs       AddApplication()
  Partnr.Workspaces.Infrastructure/   EF Core + Npgsql, ASP.NET Core Identity
    Persistence/                 ApplicationDbContext, InboxPushInterceptor (after a save that worked, tells the accounts whose inbox it touched), Configurations/, Repositories/, Seeding/, Migrations/
    Authentication/              ApplicationUser, IdentityService, CurrentUser, cookie setup
    Email/                       SmtpEmailSender (MailKit), BackgroundEmailQueue, EmailOptions
    Notifications/               NotificationEmailWorker (sends the due notification emails), NotificationOptions, the do-nothing publishers the Api replaces
    Storage/                     S3FileStorage (AWSSDK.S3, any S3-compatible service), StorageOptions
    Google/                      OAuth and Calendar clients (plain HttpClient), MeetingSyncWorker, SecretProtector (AES-GCM), FakeGoogle, GoogleOptions
    DependencyInjection.cs       AddInfrastructure()
  Partnr.Workspaces.Api/              Controllers/ (thin), Extensions/ (ControllerBaseExtensions: file downloads), Middleware/ (exception handler), Realtime/ (NotificationsHub and the SignalR publishers: the only code that knows about SignalR), Program.cs (composition root)
tests/Workspaces/
  Partnr.Workspaces.UnitTests/        Domain/, Application/ (services with fakes, request validation)
  Partnr.Workspaces.IntegrationTests/ Real Postgres via Testcontainers + WebApplicationFactory (needs Docker)
  Partnr.Workspaces.ArchitectureTests/ NetArchTest rules for the dependency rules below
```

Folders not created yet, to add when their first file appears: `Domain/ValueObjects/`, `Domain/Events/`, `Application/Validators/` (validation is data annotations on DTOs for now), `Infrastructure/Services/` (other non-persistence implementations; email and file storage have their own `Email/` and `Storage/`), `Api/Filters/`, `Api/DTOs/` (only for API-only shapes; use-case DTOs stay in Application). Do not create empty folders.

Integration tests live in a separate project so the fast unit tests can run on their own (`dotnet test tests/Workspaces/Partnr.Workspaces.UnitTests`). The architecture tests are fast too and need no database.

## Dependency rules

```
Api            -> Application, Infrastructure
Infrastructure -> Application
Application    -> Domain
Domain         -> nothing
```

- Dependencies point inward only. Domain depends on nothing external. These rules are checked by `tests/Workspaces/Partnr.Workspaces.ArchitectureTests` (`LayerDependencyTests`); add a test there when a new rule is agreed.
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

- **The browser holds a cookie; the API trusts the gateway's JWT.** The browser's session is an ASP.NET Core Identity **cookie** (HttpOnly, SameSite=Lax, 14 days sliding), set and cleared by the sign-in routes (`register|login|logout`, password reset). On every other request the **gateway** checks that cookie (a gRPC call to `Api/Internal/SessionValidationService`, which runs the same cookie handler) and forwards the request with a one-minute `Authorization: Bearer` JWT made by the gateway (`sub` = account id). The API validates only that JWT (`Api/Authentication/InternalAuthExtensions`, `InternalAuth:SigningKey`, issuer and audience from `Partnr.Contracts.InternalAuth`), and JwtBearer is the default scheme: **a request that carries only the cookie is a 401 on the API**, so the API must always be reached through the gateway. The gateway also passes on the `Set-Cookie` the session check produces (sliding renewal, or the removal of a cookie whose account is gone). Integration tests keep signing in with cookies because `tests/.../Gateway/GatewayEmulatorStartupFilter` plays the gateway in front of the test API; `InternalAuthTests` removes it to test the API as it really is. `AuthService` (the use cases: duplicate-email check, generic login error, mapping) is in `Application/Services/` and is unit-tested with a fake. It talks to Identity only through the `IIdentityService` port, implemented by a thin adapter in `Infrastructure/Authentication/` (`IdentityService`, plus `ApplicationUser`) on top of `UserManager`/`SignInManager`. It is a port, not a repository: Identity saves by itself and also owns passwords, lockout and the cookie. `ICurrentUser` is implemented by `Infrastructure/Authentication/CurrentUser` (reads the user id claim from `HttpContext`), registered in `AddInfrastructure()`.
- Endpoints: `POST /api/auth/register|login|logout`, `GET /api/auth/me`. Login failures are always the same 401 message (wrong password, unknown email and lockout are indistinguishable). 5 wrong passwords lock the account for 15 minutes.
- **The whole API requires login by default** (fallback policy in `Program.cs`). A public endpoint must be marked `[AllowAnonymous]`; a new controller needs no attribute to be protected.
- **The account behind the cookie is checked on every request** (by the gateway's session check, with `SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero`; Identity's default is 30 minutes). A cookie whose account is gone (deleted, or the development database was reseeded) is then plain anonymous: a clean 401 and the cookie is cleared. Without it such a cookie counted as signed in, so some endpoints answered 200 with empty data and `/api/auth/me` threw `UnauthorizedException` from inside the service. The cost is one primary-key lookup per request. The validator would also ask for the cookie to be re-issued on every response, and a request still in flight at sign-out then revived the session with a fresh cookie; `OnValidatePrincipal` is wrapped so `ShouldRenew` stays what the cookie handler decided from the time left (see `decisions.md`, Autentificare).
- Identity's cookie handler would redirect to a login page; it is overridden to answer 401 / 403 (an API has no login page). On the API routes JwtBearer answers 401 itself.
- CORS must keep `AllowCredentials()` or the browser will not send the cookie from `localhost:3000`.
- Roles are not global: they belong to workspace memberships (`WorkspaceMember`), so `ApplicationDbContext` extends `IdentityUserContext` (no role tables).

## Authorization (workspaces)

- Everything a freelancer or agency owns lives in a **workspace**, and its routes say so: `/api/workspaces/{workspaceId}/clients/...`. Roles (`Owner`, `Member`) belong to the membership (`WorkspaceMember`), never to the account.
- **Every service that touches data inside a workspace calls `WorkspaceAccess` first** (`Application/Authorization/WorkspaceAccess.cs`): `RequireMemberAsync` or `RequireOwnerAsync`. Do not re-implement the check in a service or a controller.
- **The client's side has its own door, `ClientAccess`** (`Application/Authorization/`): a service never looks up a project, a workspace or a client card for a client on its own. "A client may open this project" is written twice, on purpose: in C# as `Project.IsOpenToClient(client)` (messages, notifications, the unit-test fakes) and in SQL as `ProjectRepository.SharedWithClient` (lists and single reads); `ClientVisibilityRuleTests` keeps the two equal. Change one, change the other.
- **A route open to both sides resolves the caller with `CallerAccess`** (a member with a role, or a client with none; anyone else gets the usual 404), as the messages and `GET /api/workspaces/{id}` do. **The default is closed**: a route that calls `WorkspaceAccess.RequireMemberAsync` answers a client with 404, and a new one is closed to clients until someone opens it on purpose. The same route then gives each side its own answer from one service (see `decisions.md`, "Portal unificat").
- **On the firm's side, a project is found through `ProjectAccess`** (`RequireProjectAsync` to read, `RequireEditableProjectAsync` to change: 404, then 409 when archived) and the rules deliverables, updates and files share (archived is read-only, only a draft is deleted) are in `ProjectContentRules`; a service keeps only what is its own.
- A non-member gets **404**, exactly like a missing workspace, so ids cannot be probed. A member doing an owner-only action gets **403** (`ForbiddenException`), since they already know the workspace exists.
- Repositories of workspace data take the `workspaceId` in every lookup (`GetByIdAsync(workspaceId, id)`), so a record of another workspace is not found even by its id.
- Clients are not members: their portal access comes from invitations (`/api/invitations/{token}`, public to the holder of the link, except `accept` which needs the invited account) and is read through the same workspace routes as the firm's (`/api/workspaces/{id}/...`, see `CallerAccess` below) and there is no `/api/portal` any more. A client never passes `WorkspaceAccess`: an account whose card is linked sees its workspaces in `GET /api/workspaces` (with the role `Client`) and its shared projects in `GET /api/workspaces/{id}/projects[/{id}]`, in its own shape (archiving the card hides both). A project is shared when `IProjectRepository.SharedWithClient` says so: the account's card is linked and not archived, the project is not archived and has left `Planning`. A draft, an archived project, another client's project and an unknown id all answer the same 404. The client has its own DTOs (`DTOs/ClientView/`), built by `ClientProjectResponseBuilder`, and the route answers each side through a `RoleResponse` (one place decides which), so nothing of the workspace's side (client card, notes, ids of cards) can reach a client by adding a field to `ProjectResponse`.
- **Each feature's routes and rules** (workspaces, clients and their invitations, projects and stages, deliverables and approvals, updates, files, notifications, messages, the Google connection, meetings and their sync, password reset, the profile, client suggestions) are in `.claude/docs/features/`, one file per feature; the map is in the root `CLAUDE.md`. Most of them repeat the same shape: members create, edit, share and delete a draft; archiving and restoring is owner-only and sets the item back to `Draft`; an archived item and everything inside an archived project is read-only (409); something of another project is a 404; the client sees only `Shared`, non-archived items, and only after the project itself was found as shared with them.
- Members cannot be added through the API yet (invitations step); integration tests add them straight to the database.

## Adding a new feature (e.g. `Projects`)

Use `Clients` as the template. Every step is needed; nothing fails at build time if the DI lines are forgotten, the app only breaks on the first request.

1. **Domain**: `Domain/Entities/Project.cs` (private setters, static factory, throws `DomainException`); enums in `Domain/Enums/`.
2. **Application**: `Interfaces/Services/IProjectService.cs`, `Services/ProjectService.cs` (`internal`, calls `WorkspaceAccess` first if the data belongs to a workspace), `Interfaces/Repositories/IProjectRepository.cs`, `Mappings/ProjectMappings.cs`, `DTOs/Projects/` (requests and responses). One repository per aggregate, not per entity.
3. **Application DI**: register `IProjectService` in `Application/DependencyInjection.cs`.
4. **Infrastructure**: `DbSet<Project>` in `ApplicationDbContext`, `Persistence/Configurations/ProjectConfiguration.cs`, `Persistence/Repositories/ProjectRepository.cs`, and register `IProjectRepository` in `Infrastructure/DependencyInjection.cs`.
5. **Migration**: `dotnet ef migrations add <Name> ...` (see Database and migrations), then review the generated file.
6. **Api**: `Controllers/ProjectsController.cs`, thin, no error handling.
7. **Tests**: service tests in `tests/Workspaces/Partnr.Workspaces.UnitTests/Application/` (fake repository + fake unit of work), entity tests in `Domain/`.
8. Run `dotnet build` (0 warnings) and `dotnet test`.
9. **Docs**: describe the feature's routes and rules in `.claude/docs/features/<feature>.md` and add it to the map in the root `CLAUDE.md`.

External services (email, OAuth, AI, file storage) follow the same rule as repositories: the interface lives in `Application/Interfaces/Services/`, the implementation next to its kind of service in Infrastructure (`Email/`, `Storage/`, ...), never in `Persistence/`.

## Database and migrations

- PostgreSQL 17 via `docker-compose.yml` (db `partnr`, local-dev-only credentials `postgres`/`postgres`, port 5432). The `Postgres` connection string is in `src/Services/Workspaces/Partnr.Workspaces.Api/appsettings.Development.json`. To use a different Postgres (e.g. one installed locally), override it with user secrets instead of editing that file: `dotnet user-secrets set "ConnectionStrings:Postgres" "Host=...;Password=..." --project src/Services/Workspaces/Partnr.Workspaces.Api`.
- **Email** goes through `IEmailSender` (Application), implemented by `Infrastructure/Email/SmtpEmailSender` (MailKit) and configured by the `Email` section (`From`, `SmtpHost`, `SmtpPort`, `SmtpUsername`, `SmtpPassword`, `UseTls`), validated at startup. In development the SMTP server is **Mailpit** from `docker-compose.yml` (SMTP on 1025, inbox UI on http://localhost:8025): every email is caught there, none reaches a real inbox. Production only changes the configuration. Integration tests replace the sender with `FakeEmailSender` (`ApiFactory.Emails`); `Email/SmtpEmailSenderTests` checks the real sender against a Mailpit container.
- **File storage** goes through `IFileStorage` (Application), implemented by `Infrastructure/Storage/S3FileStorage` (AWSSDK.S3) and configured by the `Storage` section (`ServiceUrl`, `Region`, `Bucket`, `AccessKey`, `SecretKey`, `ForcePathStyle`, `CreateBucketIfMissing`), validated at startup. Any S3-compatible service works (AWS S3, R2, B2): production only changes the configuration and makes the bucket ahead of time. In development it is **RustFS** from `docker-compose.yml` (S3 API on http://localhost:9000, console on http://localhost:9001, dev credentials `rustfsadmin`/`rustfsadmin`, bucket `partnr` created on first use); MinIO no longer publishes images. `DeleteAsync` never throws (it logs), `OpenReadAsync` throws `NotFoundException` for a missing key. Integration tests replace the storage with `InMemoryFileStorage` (`ApiFactory.Files`); `Storage/S3FileStorageTests` checks the real adapter against a RustFS container.
- Migrations are created in Infrastructure, started from Api (commands work in any shell):

```bash
dotnet ef migrations add <Name> --project src/Services/Workspaces/Partnr.Workspaces.Infrastructure --startup-project src/Services/Workspaces/Partnr.Workspaces.Api --output-dir Persistence/Migrations
dotnet ef database update --project src/Services/Workspaces/Partnr.Workspaces.Infrastructure --startup-project src/Services/Workspaces/Partnr.Workspaces.Api
```

## Common commands

```bash
docker compose up -d                                              # Postgres + Mailpit (http://localhost:8025) + RustFS (S3 on :9000, console :9001)
dotnet build                                                      # must be 0 warnings
dotnet test
dotnet run --project src/Services/Workspaces/Partnr.Workspaces.Api --launch-profile http   # http://localhost:5051
dotnet run --project src/Services/Workspaces/Partnr.Workspaces.Api --launch-profile http -- --seed   # test data, then exits
dotnet run --project src/Gateway/Partnr.Gateway --launch-profile http   # http://localhost:5050, what the frontend calls
```

**Gateway** (`src/Gateway/Partnr.Gateway`, tests in `tests/Gateway/`): a YARP reverse proxy and the only entry point to the backend. It forwards `/api/*` to the API (routes and destination in `appsettings.json`, under `ReverseProxy`; override the address with `ReverseProxy__Clusters__workspaces__Destinations__api__Address`). On every request that carries the session cookie (`Sessions/SessionMiddleware`) it asks the API over gRPC (`Services:Workspaces:GrpcAddress`, `Sessions/GrpcSessionValidator`) whether the cookie is good, and if so adds `Authorization: Bearer <JWT>` (one minute, signed with `InternalAuth:SigningKey`, claim `sub` = account id; `Sessions/InternalTokenIssuer`). An `Authorization` header from the caller is always dropped. If the API cannot be asked the answer is 503, not "signed out". The API accepts only that JWT (see Authentication below). The gateway also passes on the `Set-Cookie` of the session check (renewal, removal). It holds no business logic and references no service project, only `Partnr.Contracts`.

**gRPC (internal)**: the API has a second Kestrel endpoint for gRPC, HTTP/2 only (cleartext HTTP/2 needs its own port): `Kestrel:Endpoints` in `appsettings.Development.json` defines both `Http` (5051) and `Grpc` (5052), and because they are defined there `ASPNETCORE_URLS` is ignored. The port is internal: the gateway does not route to it and it must not be reachable from outside. Contracts (`.proto` files and the shape of the internal JWT) live in `src/BuildingBlocks/Partnr.Contracts`, which has no logic. The gRPC service sits next to the controllers in `Api/Internal/` and is `[AllowAnonymous]` because its caller is the one asking for proof.

- **Test data (development only):** `-- --seed` (VS Code task `db-seed`) applies pending migrations and adds demo accounts, workspaces and clients, then exits. It is never automatic, so the e2e and integration databases stay empty. It is safe to run again: when `demo@example.com` exists it does nothing. **F5 ("API" launch profile) reseeds on its own when the seed changed**: it sets `Seed__ReseedOnChange=true`, and at startup the API compares a fingerprint of the seed's source (`SeedFingerprint`: `DevelopmentSeed.cs` and `DevelopmentDataSeeder.cs`, embedded in the assembly at build) with the one stored as the database's own comment (`COMMENT ON DATABASE`, no table). If they differ, or the database has demo data but no fingerprint, **every table is dropped (`DROP SCHEMA public CASCADE`), recreated by the migrations and seeded again, so anything entered by hand is lost**; it refuses a database that is not on localhost. It is off by default because the integration and e2e runs are in Development too; `dotnet run` without that variable never recreates anything (use `-- --seed` by hand). Seeding writes files to the storage, so RustFS must be running. Stop the running API first (a debugger locks the DLLs `dotnet run` has to rebuild). The accounts (`demo@example.com`, password `demo-password`, and others) and everything the seed creates are listed in `.claude/docs/features/seed-data.md`; keep that list in step with `DevelopmentDataSeeder.cs`.

- API docs (Development only): Scalar at `http://localhost:5050/scalar/v1`, spec at `/openapi/v1.json`, both **through the gateway** (a Development-only route in its `appsettings.Development.json` that passes the original Host on, so the spec names the gateway as the server). Sign in to the web app first: the browser sends its session cookie to any port of `localhost`, the gateway turns it into the JWT, and "Try it" works on authenticated routes. On `:5051` the docs load but authenticated calls are a 401.
- CORS allows `http://localhost:3000` (frontend).
- Before saying a change is done: `dotnet build` with zero warnings and a green `dotnet test`.

## Prerequisites

- .NET SDK 10 (version pinned in `global.json`), the `dotnet-ef` tool (`dotnet tool install --global dotnet-ef`), Docker (for Postgres, Mailpit and RustFS).
- If `docker compose up` fails with a network timeout while pulling images, retry before reporting a failure.
