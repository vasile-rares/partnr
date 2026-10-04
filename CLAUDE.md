# Client Portal

Monorepo with two applications:

- `backend/` — .NET 10 API (Clean Architecture), PostgreSQL
- `frontend/` — Next.js 16 (React 19, Tailwind 4, TypeScript)

Code, identifiers, code comments and commit messages are in English. Reply to the user in the language they write in.

## Working rules

- **After every change, suggest a short commit message** in case the user wants to commit right then. Format: `type: summary` (e.g. `feat: add client registration endpoint`), single line, imperative mood. Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`.
- **Never run `git commit` / `git push` on your own.** Only on explicit request; just propose the message.
- Do not run destructive commands (reset --hard, deleting folders, `docker compose down -v`) without confirmation.
- Verify before saying it's done: `dotnet build` with zero warnings and a green `dotnet test` for the backend; `npm run lint` for the frontend.
- If something could not be tested (e.g. Docker/DB unavailable), say so explicitly.
- Personal preferences that should not apply to everyone belong in `CLAUDE.local.md` (git-ignored), not here.

## Backend (`backend/`)

Structure, dependency rules, conventions, database and commands are in [backend/CLAUDE.md](backend/CLAUDE.md), which Claude Code loads when working in that folder. The structure follows [this article](https://dev.to/mashrulhaque/how-to-design-a-maintainable-net-solution-structure-for-growing-teams-284n).

## Frontend (`frontend/`)

- Next.js 16 has **breaking changes** compared to what you know. Before writing code, read the relevant guide in `frontend/node_modules/next/dist/docs/` (see `frontend/AGENTS.md`).
- Commands (from `frontend/`): `npm run dev` (localhost:3000), `npm run build`, `npm run lint`.
- Calls the backend API at `http://localhost:5051`.

## VS Code (`.vscode/`)

- Launch: **API**, **Frontend**, **Full stack** (compound, F5).
- Tasks: `build-api`, `test` (default), `db-up`, `db-migrate`. `db-up` / `db-migrate` are not tied to F5.

## Prerequisites

- Backend: .NET SDK 10, `dotnet-ef`, Docker (details in `backend/CLAUDE.md`). Frontend: Node.js + npm.
- Works on Windows, macOS and Linux; use forward slashes and shell-agnostic commands in docs and scripts.
