# Partnr

Monorepo with two applications:

- `backend/` — .NET 10 API (Clean Architecture), PostgreSQL
- `frontend/` — Next.js 16 (React 19, Tailwind 4, TypeScript)

Code, identifiers, code comments and commit messages are in English. Reply to the user in the language they write in.

## Product plan

The product vision, feature list and MVP scope are in [.claude/docs/product-plan.md](.claude/docs/product-plan.md) (written in Romanian). Read it at the start of a new feature or when unsure about scope. The decisions already taken (with their reasons) are in [.claude/docs/decisions.md](.claude/docs/decisions.md), one section per area: read only the section relevant to the current step.

- The user builds this **step by step, to understand it and catch problems along the way**. Work in small steps, explain the why briefly, and do not jump ahead to features outside the current step or build the whole MVP at once.
- After finishing a step, tick it in the `Progres` section of the plan; record architecture / data-model decisions in the matching section of `decisions.md`.
- Stick to the MVP list; proposals, payments, advanced automations, CRM, AI, white-label and extra integrations come later.

## Feature docs (`.claude/docs/features/`)

How each built feature works, API and web app side by side in one file: routes, rules, components, and how the e2e tests find things. Read only the file of the feature being worked on; this file and `backend/CLAUDE.md` keep only the rules that apply everywhere.

**They describe the code as it is today, they do not constrain it.** Numbers, labels, routes, component names and behaviours in them are a snapshot, not requirements: when the user asks for a change that contradicts a feature doc, make the change and update the doc. Only the rules in this file and in `backend/CLAUDE.md` (architecture, security, conventions, working rules) are binding, and even those change when the user decides so. If the code and a feature doc disagree, the code wins and the doc is fixed.

| Feature | File |
| --- | --- |
| Sign-in flows, password reset, account page and profile | [auth.md](.claude/docs/features/auth.md) |
| Workspaces, members' roles, workspace settings | [workspaces.md](.claude/docs/features/workspaces.md) |
| Sidebar, menus, page frame, theme | [app-shell.md](.claude/docs/features/app-shell.md) |
| Clients, account suggestions | [clients.md](.claude/docs/features/clients.md) |
| Client invitations, locked client details | [invitations.md](.claude/docs/features/invitations.md) |
| Projects, stages, project page, timeline | [projects.md](.claude/docs/features/projects.md) |
| Deliverables, approvals | [deliverables-and-approvals.md](.claude/docs/features/deliverables-and-approvals.md) |
| Project updates | [updates.md](.claude/docs/features/updates.md) |
| Project files | [files.md](.claude/docs/features/files.md) |
| Invitations, the client's portal | [portal.md](.claude/docs/features/portal.md) |
| Notifications, bell, live push | [notifications.md](.claude/docs/features/notifications.md) |
| Messages | [messages.md](.claude/docs/features/messages.md) |
| Dashboard, calendar | [dashboard-and-calendar.md](.claude/docs/features/dashboard-and-calendar.md) |
| Google Calendar connection | [google-calendar.md](.claude/docs/features/google-calendar.md) |
| Meetings, sync to Google | [meetings.md](.claude/docs/features/meetings.md) |
| Development seed data | [seed-data.md](.claude/docs/features/seed-data.md) |

- When a step changes how a feature behaves, update its file in the same change. A new feature gets a new file and a row here.
- Describe behaviour, rules and where things live, not code that can be read: no lists of every file or field.

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
- **Auth**: `src/proxy.ts` (Next 16's name for middleware) only does a quick redirect when the session cookie is missing. The real check is `requireUser()` (calls `GET /api/auth/me`), which **every protected page must call itself**: layouts are not a safe place for it, see the Next.js authentication guide. A new public page is added to the `proxy.ts` matcher exclusions.
- **Workspace pages** live under `src/app/w/[workspaceId]/` and call `getWorkspace()` (`src/lib/workspaces/server.ts`), showing `notFound()` when it returns null. Server-side API calls go through `serverFetch` (`src/lib/api/server.ts`), browser-side ones through `sendJson` / `sendForm` (`src/lib/api/client.ts`).
- **Pages** sit in the app shell's sidebar (`src/components/shell/`). A page starts with `<Page crumbs={[...]}>` and must not render its own `<main>` (the shell's `SidebarInset` is it). Layouts do not re-render on navigation, so a mutation that changes what the sidebar shows (a new project) calls `router.refresh()`.
- **Adding to a page's sections** (Updates, Deliverables, Files, and the stages on the project bar) is asked for, never left on the page: a button in the section header (and "Add the first …" in the empty message; Files has a permanent drop area instead) opens a dialog with the form; in e2e tests look for the fields in `page.getByRole("dialog", { name: "Add an update" })` (or "Add a deliverable", "Upload a file", "Add a stage").
- **E2E tests** (Playwright, in `frontend/e2e/`): `pnpm test:e2e` from `frontend/`. One-time setup: `pnpm exec playwright install chromium`. Nothing needs to be started by hand, but Postgres, Mailpit and the file storage (RustFS) must be running (`docker compose up -d` in `backend/`; the run stops with a clear message if Mailpit or the storage is missing, because invitations and notifications send real SMTP (the API's worker sends notification emails every 5 s) and the tests read the emails back from it, and the file tests upload to the bucket `partnr-e2e`, created on first use), with the dev credentials (user secrets or `appsettings.Development.json`) and `dotnet-ef` installed. A run compiles the API into `frontend/e2e/.artifacts`, **recreates the separate database `partnr_e2e`** (never the dev one: `e2e/start-api.mjs` refuses any database name that does not end in `_e2e`), starts the API on port 5151 and a production build of the frontend on 3100, so it can run while the dev servers are up. Override the database with `E2E_POSTGRES_CONNECTION`. Add a test when a new user-facing flow lands.
- **Components are organised by feature, with the roles inside it** (the same idea as the backend: one feature, two doors). A feature folder in `src/components/` (`projects/`, `deliverables/`, `meetings/`, ...) holds everything of that feature for members and clients; there is no folder or `lib/` module for "the client's app". A page picks its screen by `workspace.role` (`Client` or not), and the two screens stay separate components because they are different things (`FirmProjectView` edits, `ClientProjectView` reads and answers), even where they look alike. Naming: what only a client sees or does is `Client*` (`ClientApprovalPanel`, `ClientProject`, `getClientProject`); what members see is `Firm*` when the client has a counterpart of the same concept (`FirmUpdateList` / `ClientUpdateList`, `FirmProjectView` / `ClientProjectView`) and unprefixed when it exists only for members (`ClientsTable`, `ProjectForm`); what both use has no role in its name (`ProjectTimeline`, `ProjectTabs`, badges). Types and fetchers of both shapes live side by side in the feature's `lib/` folder (`lib/projects/types.ts` has `Project` and `ClientProject`).
- **Share by composing small pieces, not by one component with many flags.** Repeated markup goes into a small shared component that takes `children` (`components/layout/`: `Section` for a titled block, `DividedList` for the bordered list of rows; `JoinMeetingButton` in `meetings/`), and logic both roles need goes into one function (`getClientProjects` in `lib/projects/server.ts`). Do not merge a member component and a client component into one that switches on a `role` prop: extract what they share and let each compose it.
- **UI is built with [shadcn/ui](https://ui.shadcn.com)** on Base UI primitives, style `base-nova` (see `components.json`). Add components with `pnpm dlx shadcn@latest add <name>` (from `frontend/`); they land in `src/components/ui/` and are only edited on purpose. Use them before writing custom UI.
  - Base UI, not Radix: composition uses the `render` prop, not `asChild`. A link that looks like a button is `<Link className={buttonVariants()}>`, not `<Button render={<Link />}>` (Base UI's Button would replace the link role).
  - Theme tokens live in `src/app/globals.css` (switch preset with `pnpm dlx shadcn@latest apply <code>`). The only custom tokens are `--status-done/doing/todo` (project step colours). Dark mode is the `dark` class set by `next-themes` (default; `ThemeRadioGroup` in the account menu of the sidebar, `ThemeToggle` on the sign-in pages).
  - Row actions (share, edit, archive, delete, move, download) are icon buttons with a tooltip: `IconButton` / `IconLink` in `src/components/actions/`. Always pass an `aria-label` with the item's name; the tooltip is only the short verb. Tooltips never open on touch (a tap runs the action), so anything a phone user has to read goes in an `InfoTip` (`src/components/actions/InfoTip.tsx`): a popover that opens on hover, focus and tap.
  - Forms keep `useActionState` + native validation (rules live in the API); `components/forms/TextField` wraps shadcn's `Field`/`Input`, `FormError` wraps `Alert`.
- **Structural elements are named in `className`**: the first class is a kebab-case name, before the Tailwind ones (`needs-attention-list flex flex-col divide-y ...`; with `cn(...)` it is the first string argument), so the element is recognisable in DevTools. Name = component + role (`project-timeline`, `project-timeline-stage`); it goes on a component's root, sections, lists and their items, cards, forms, dialogs / sheets / popovers and layout wrappers, not on text spans, icons or one-off wrappers. The name has no CSS behind it, so keep it clear of the real classes in `globals.css` (`timeline-line`, `timeline-step`). Not in `src/components/ui/` (shadcn).
- UI text is in English.

## Skills (`.claude/skills/`)

Project rules in this file take precedence over any skill. A skill never authorizes `git commit` / `git push`.

- **`grilling`** (`/grill-me`) — before implementing a new feature or making an architecture decision (auth, data model, roles, API contract), grill the user on the plan first. Skip it for small fixes and obvious changes.
- **`react-best-practices`** — apply when writing, reviewing or refactoring React / Next.js code in `frontend/` (components, pages, data fetching, bundle size). Where it conflicts with the Next.js 16 docs in `node_modules/next/dist/docs/`, the docs win.
- **`shadcn`** (official, installed with `pnpm dlx skills add shadcn/ui --skill shadcn --agent claude-code --copy`; to update it, run the same command again) — apply when adding, composing or styling UI in `frontend/`: shadcn CLI usage, Base UI vs Radix, form and composition rules. Project rules here and the Next.js 16 docs take precedence over it.
- **`web-design-guidelines`** (Vercel's, installed with `pnpm dlx skills add vercel-labs/agent-skills --skill web-design-guidelines --agent claude-code --copy`; to update it, run the same command again) — apply when reviewing or auditing `frontend/` UI for accessibility, UX and Web Interface Guidelines compliance ("review my UI", "check accessibility"). It fetches the current rules from GitHub at review time, so it needs network access. A review skill: it reports findings as `file:line`, and fixes are made only when asked.
- **`frontend-design`** — apply when building a new UI or reshaping an existing one visually and no design is provided. Do not use it when a Figma design or an existing design system defines the look; follow that instead.

## VS Code (`.vscode/`)

- Launch: **API** (debugger) and **Full stack** (compound, F5: runs the `frontend` task, plain `pnpm dev` in its own terminal, then the API under the debugger). The frontend is not debugged from VS Code and keeps running when the API session stops.
- Tasks: `frontend`, `build-api`, `test` (default), `db-up`, `db-migrate`, `db-seed`. The `db-*` tasks are not tied to F5.

## Prerequisites

- Backend: .NET SDK 10, `dotnet-ef`, Docker for Postgres, Mailpit (the local email catcher) and RustFS (the local S3-compatible file storage); details in `backend/CLAUDE.md`. Frontend: Node.js + pnpm (the version is pinned in `frontend/package.json`; install with `npm install -g pnpm`; Corepack 0.31 cannot run pnpm 12).
- Works on Windows, macOS and Linux; use forward slashes and shell-agnostic commands in docs and scripts.
