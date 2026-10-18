# Client Portal

Monorepo with two applications:

- `backend/` — .NET 10 API (Clean Architecture), PostgreSQL
- `frontend/` — Next.js 16 (React 19, Tailwind 4, TypeScript)

Code, identifiers, code comments and commit messages are in English. Reply to the user in the language they write in.

## Product plan

The product vision, feature list and MVP scope are in [.claude/docs/product-plan.md](.claude/docs/product-plan.md) (written in Romanian). Read it at the start of a new feature or when unsure about scope. The decisions already taken (with their reasons) are in [.claude/docs/decisions.md](.claude/docs/decisions.md), one section per area: read only the section relevant to the current step.

- The user builds this **step by step, to understand it and catch problems along the way**. Work in small steps, explain the why briefly, and do not jump ahead to features outside the current step or build the whole MVP at once.
- After finishing a step, tick it in the `Progres` section of the plan; record architecture / data-model decisions in the matching section of `decisions.md`.
- Stick to the MVP list; proposals, payments, advanced automations, CRM, AI, white-label and extra integrations come later.

## Working rules

- **After every change, suggest a short commit message** in case the user wants to commit right then. Format: `type: summary` (e.g. `feat: add client registration endpoint`), single line, imperative mood. Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`.
- **Never run `git commit` / `git push` on your own.** Only on explicit request; just propose the message.
- **Comments are sparse and short.** Write one only for a non-obvious *why* (a security reason, a workaround, an invariant the types cannot show), one line when possible. No comment that restates the code or a name, no `<exception>` / `<param>` boilerplate on interfaces, no narration of steps. A better name beats a comment.
- Do not run destructive commands (reset --hard, deleting folders, `docker compose down -v`) without confirmation.
- Verify before saying it's done: `dotnet build` with zero warnings and a green `dotnet test` for the backend; `pnpm lint` for the frontend.
- If something could not be tested (e.g. Docker/DB unavailable), say so explicitly.
- Personal preferences that should not apply to everyone belong in `CLAUDE.local.md` (git-ignored), not here.

## Backend (`backend/`)

Structure, dependency rules, conventions, database and commands are in [backend/CLAUDE.md](backend/CLAUDE.md), which Claude Code loads when working in that folder. The structure follows [this article](https://codewithmukesh.com/blog/clean-architecture-dotnet/).

## Frontend (`frontend/`)

- Next.js 16 has **breaking changes** compared to what you know. Before writing code, read the relevant guide in `frontend/node_modules/next/dist/docs/` (see `frontend/AGENTS.md`).
- Commands (from `frontend/`): `pnpm dev` (localhost:3000), `pnpm build`, `pnpm lint`.
- Calls the backend API at `http://localhost:5051` (override with the `BACKEND_URL` env var). The browser never calls it directly: `next.config.ts` rewrites `/api/*` to the backend, so the session cookie belongs to the frontend's own origin and no CORS is involved. Server Components call the backend directly with the session cookie forwarded.
- **Auth**: pages and forms are in `src/app/(auth)/` and `src/components/auth/`; the session logic is in `src/lib/auth/`. `src/proxy.ts` (Next 16's name for middleware) only does a quick redirect when the session cookie is missing. The real check is `requireUser()` (calls `GET /api/auth/me`), which every protected page must call itself: layouts are not a safe place for it, see the Next.js authentication guide.
- **Workspaces**: `/` only redirects (no workspace → `/workspaces/new`, one → `/w/{id}`, several → `/workspaces`). Workspace pages live under `src/app/w/[workspaceId]/` and call `getWorkspace()` (`src/lib/workspaces/server.ts`), showing `notFound()` when it returns null. Server-side API calls go through `serverFetch` (`src/lib/api/server.ts`), browser-side ones through `sendJson` (`src/lib/api/client.ts`).
- **Projects**: `src/app/w/[workspaceId]/projects/` (list, `new` with `?clientId=` to preselect, `[projectId]`), components in `src/components/projects/`, data in `src/lib/projects/`. A client's page lists that client's projects with the same `ProjectsTable`.
- **Deliverables**: the "Deliverables" section on the project page (`src/components/deliverables/`, data in `src/lib/deliverables/`). `getDeliverables()` loads archived ones too; the owner restores them from the same list.
- **Updates** (the timeline's written notes): the "Updates" section on the project page, under Deliverables (`src/components/updates/`, data in `src/lib/updates/`). Same shape as deliverables: `getProjectUpdates()` loads archived ones too, the owner restores them from the same list.
- **Clients' side**: `/invite/[token]` (in `src/app/(auth)/`, public, listed in the `proxy.ts` matcher exclusions) opens an invitation and `/portal` is where a client lands: their workspaces with the projects shared with them (`getPortalWorkspaces()`, `getPortalProjects()` in `src/lib/portal/server.ts`), and `/portal/projects/[projectId]` is one project with its shared updates (the five latest open, the rest in a `<details>`), its shared deliverables and its stages (`getPortalProject()`, wording in `src/lib/portal/summary.ts`). `/` sends someone with no workspace of their own but a client link to `/portal`.
- **E2E tests** (Playwright, in `frontend/e2e/`): `pnpm test:e2e` from `frontend/`. One-time setup: `pnpm exec playwright install chromium`. Nothing needs to be started by hand, but Postgres and Mailpit must be running (`docker compose up -d` in `backend/`; the run stops with a clear message if Mailpit is missing, because invitations send real SMTP and the tests read the emails back from it), with the dev credentials (user secrets or `appsettings.Development.json`) and `dotnet-ef` installed. A run compiles the API into `frontend/e2e/.artifacts`, **recreates the separate database `client_portal_e2e`** (never the dev one: `e2e/start-api.mjs` refuses any database name that does not end in `_e2e`), starts the API on port 5151 and a production build of the frontend on 3100, so it can run while the dev servers are up. Override the database with `E2E_POSTGRES_CONNECTION`. Add a test when a new user-facing flow lands.
- **UI is built with [shadcn/ui](https://ui.shadcn.com)** on Base UI primitives, style `base-nova` (see `components.json`). Add components with `pnpm dlx shadcn@latest add <name>` (from `frontend/`); they land in `src/components/ui/` and are only edited on purpose. Use them before writing custom UI.
  - Base UI, not Radix: composition uses the `render` prop, not `asChild`. A link that looks like a button is `<Link className={buttonVariants()}>`, not `<Button render={<Link />}>` (Base UI's Button would replace the link role).
  - Theme tokens live in `src/app/globals.css` (switch preset with `pnpm dlx shadcn@latest apply <code>`). The only custom tokens are `--status-done/doing/todo` (project step colours). Dark mode is the `dark` class set by `next-themes` (`ThemeToggle` in the header and on the sign-in pages).
  - Forms keep `useActionState` + native validation (rules live in the API); `components/forms/TextField` wraps shadcn's `Field`/`Input`, `FormError` wraps `Alert`.
- UI text is in English.

## Skills (`.claude/skills/`)

Project rules in this file take precedence over any skill. A skill never authorizes `git commit` / `git push`.

- **`grilling`** (`/grill-me`) — before implementing a new feature or making an architecture decision (auth, data model, roles, API contract), grill the user on the plan first. Skip it for small fixes and obvious changes.
- **`react-best-practices`** — apply when writing, reviewing or refactoring React / Next.js code in `frontend/` (components, pages, data fetching, bundle size). Where it conflicts with the Next.js 16 docs in `node_modules/next/dist/docs/`, the docs win.
- **`shadcn`** (official, installed with `pnpm dlx skills add shadcn/ui --skill shadcn --agent claude-code --copy`; to update it, run the same command again) — apply when adding, composing or styling UI in `frontend/`: shadcn CLI usage, Base UI vs Radix, form and composition rules. Project rules here and the Next.js 16 docs take precedence over it.
- **`web-design-guidelines`** (Vercel's, installed with `pnpm dlx skills add vercel-labs/agent-skills --skill web-design-guidelines --agent claude-code --copy`; to update it, run the same command again) — apply when reviewing or auditing `frontend/` UI for accessibility, UX and Web Interface Guidelines compliance ("review my UI", "check accessibility"). It fetches the current rules from GitHub at review time, so it needs network access. A review skill: it reports findings as `file:line`, and fixes are made only when asked.
- **`frontend-design`** — apply when building a new UI or reshaping an existing one visually and no design is provided. Do not use it when a Figma design or an existing design system defines the look; follow that instead.

## VS Code (`.vscode/`)

- Launch: **API**, **Frontend**, **Full stack** (compound, F5).
- Tasks: `build-api`, `test` (default), `db-up`, `db-migrate`, `db-seed`. The `db-*` tasks are not tied to F5.

## Prerequisites

- Backend: .NET SDK 10, `dotnet-ef`, Docker for Postgres and Mailpit, the local email catcher (details in `backend/CLAUDE.md`). Frontend: Node.js + pnpm (the version is pinned in `frontend/package.json`; install with `npm install -g pnpm`; Corepack 0.31 cannot run pnpm 12).
- Works on Windows, macOS and Linux; use forward slashes and shell-agnostic commands in docs and scripts.
