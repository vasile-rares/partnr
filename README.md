# Partnr

A project and client collaboration platform for freelancers and small agencies. The freelancer manages the work; the client always knows what is going on.

> [!WARNING]
> This project is **under active development and not finished**. Large parts of the plan (notifications, messaging, proposals, payments, automations and more) are not built yet, and things may change or break without notice.

## Overview

Freelancers find clients elsewhere, add them here, and run each project from one place. Clients get a simple portal where they can see, at any time:

- where the project stands and what comes next;
- the deliverables and files shared with them;
- the written updates from the freelancer;
- what is waiting for their answer, and approve a deliverable or request changes.

The same person can be a member of one workspace and a client in another, with one account.

## Features

Built so far:

- Sign up and sign in (cookie session), account profile
- Workspaces, with owner and member roles
- Client cards, archiving, and invitations by email
- Projects with stages, status, dates and a timeline bar edited in place
- Deliverables (links) with approval rounds: the client approves or requests changes with a message
- Written project updates (draft or shared)
- File uploads (S3-compatible storage), shared with the client per file
- Freelancer dashboard and sidebar app shell, dark theme by default
- Client portal per firm: home, projects, deliverables, updates and files

## Roadmap

The most important things still to come, roughly in this order:

- **Notifications**: email first, then in the app (the last piece of the MVP)
- **Messages**: project communication in one place instead of email and chat apps
- **Calendar**: project and stage deadlines in a calendar, with Google Calendar and Meet links for meetings
- **Kanban**: a board view of the project stages
- **Automations**: "when X happens, do Y", such as reminders for deliverables a client has not approved
- **Proposals and payments**: send a proposal the client can accept, and show payment status through Stripe
- **White-label**: the portal with the freelancer's own logo, name and colours
- **Integrations**: GitHub and Figma

Smaller ideas (CRM, AI-assisted updates, stage templates, a public API) are left out here. The full plan and the progress checklist are in [.claude/docs/product-plan.md](.claude/docs/product-plan.md) (written in Romanian).

## Tech stack

| Part     | Stack                                                                                                |
| -------- | ---------------------------------------------------------------------------------------------------- |
| Backend  | .NET 10, ASP.NET Core Identity, EF Core, PostgreSQL 17, Clean Architecture (see [backend/CLAUDE.md](backend/CLAUDE.md)) |
| Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS 4, [shadcn/ui](https://ui.shadcn.com) on Base UI         |
| Local services | Docker Compose: Postgres, [Mailpit](https://mailpit.axllent.org) (email catcher), [RustFS](https://github.com/rustfs/rustfs) (S3-compatible storage), [RabbitMQ](https://www.rabbitmq.com) (events between services) |
| Tests    | xUnit (unit, integration with Testcontainers, architecture), Playwright (end to end)                  |

```
backend/    .NET API: Domain, Application, Infrastructure, Api + tests
frontend/   Next.js app (freelancer app and client portal) + Playwright e2e tests
```

The browser never calls the API directly: Next.js rewrites `/api/*` to the backend, so the session cookie stays on the frontend's origin.

## Getting started

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download) and the EF Core tool: `dotnet tool install --global dotnet-ef`
- [Node.js](https://nodejs.org) and [pnpm](https://pnpm.io) (`npm install -g pnpm`)
- [Docker](https://www.docker.com/)

### Run it locally

1. Start Postgres, Mailpit, RustFS and RabbitMQ:

   ```bash
   cd backend
   docker compose up -d
   ```

2. Start the API (http://localhost:5051), and optionally load demo data:

   ```bash
   dotnet run --project src/Services/Workspaces/Partnr.Workspaces.Api --launch-profile http
   # demo data, then exits (safe to run again)
   dotnet run --project src/Services/Workspaces/Partnr.Workspaces.Api --launch-profile http -- --seed
   ```

3. Start the Integrations service (http://localhost:5053; Google: the connection and the sending of meetings; it creates its own database `partnr_integrations`), and the gateway (http://localhost:5050), the only door the frontend talks to, each in its own terminal:

   ```bash
   dotnet run --project src/Services/Integrations/Partnr.Integrations.Api --launch-profile http
   dotnet run --project src/Gateway/Partnr.Gateway --launch-profile http
   ```

   Optional: `aspire run` in `backend/` starts the three with the [Aspire](https://aspire.dev) dashboard (logs, metrics and traces of the backend); it needs the Aspire CLI (`dotnet tool install -g Aspire.Cli`) and replaces steps 2 and 3, except for loading demo data.

4. Start the frontend (http://localhost:3000):

   ```bash
   cd ../frontend
   pnpm install
   pnpm dev
   ```

With the demo data, sign in with password `demo-password` as:

| Account              | Role                                    |
| -------------------- | --------------------------------------- |
| `demo@example.com`   | Owner of "Studio Pop" and "Side Project" |
| `member@example.com` | Member of "Studio Pop"                  |
| `client@example.com` | Client of "Studio Pop" (the portal)     |

Useful local addresses: RabbitMQ management UI at http://localhost:15672 (partnr / partnr), API docs at http://localhost:5050/scalar/v1 (through the gateway; sign in to the app first and "Try it" works), caught emails at http://localhost:8025, storage console at http://localhost:9001.

> [!NOTE]
> The credentials in `docker-compose.yml` and `appsettings.Development.json` are for local development only.

## Tests

```bash
# backend (the integration tests need Docker)
cd backend
dotnet test

# frontend
cd frontend
pnpm lint
pnpm exec playwright install chromium   # once
pnpm test:e2e
```

The e2e run builds the API and the frontend itself and uses a separate `partnr_e2e` database, never the development one. Postgres, Mailpit, RustFS and RabbitMQ must be running.

## Documentation

- [.claude/docs/product-plan.md](.claude/docs/product-plan.md): product vision, MVP scope and progress
- [.claude/docs/decisions.md](.claude/docs/decisions.md): architecture and data-model decisions, with their reasons
- [backend/CLAUDE.md](backend/CLAUDE.md): backend structure, conventions and authorization rules
