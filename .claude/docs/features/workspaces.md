# Workspaces

Who may touch a workspace (`WorkspaceAccess`, 404 for a non-member, 403 for a member doing an owner-only action) is in `backend/CLAUDE.md` ("Authorization"). Decisions: `decisions.md`, "Workspace".

## API

- `/api/workspaces` (`WorkspaceService`): `POST` creates one with the caller as its `Owner`, `GET` lists the caller's own, the ones they work in first and then the ones they are a client of (each with the caller's `role`, `Client` for the latter; an archived client's firm is left out), `GET {id}` answers a member (with their role) and also a client of the workspace (with the role `Client`, the only thing a client can do on `/api/workspaces/{id}` itself), `PATCH {id}` renames it and is **owner-only**.
- The name is required and at most 100 characters (`Workspace.NameMaxLength`, checked in the entity).
- Members are `WorkspaceMember` rows inside the `Workspace` aggregate, with a role (`Owner`, `Member`). The owner adds members by invitation and removes them (the team's routes, rules and page are in [team.md](team.md)); the seed and some integration tests still add them straight to the database.
- A member of a workspace cannot also be its client (accepting an invitation answers 409, see [invitations.md](invitations.md)).

## Web app

- `/` only redirects, counting every workspace the person can open, as a member or as a client: none → `/workspaces/new`, one → `/w/{id}`, several → `/workspaces`.
- `/workspaces` (`app/(shell)/workspaces/page.tsx`) lists every workspace the person can open, with their role and links to each; `/workspaces/new` holds `CreateWorkspaceForm`. Both sit in the shell with `kind: "none"` ([app-shell.md](app-shell.md)).
- `/w/[workspaceId]/settings`: the owner renames the workspace (`RenameWorkspaceForm`); a member only sees the name and is told only the owner can rename it.
- Data in `src/lib/workspaces/`: `getOpenWorkspace()` opens a workspace as a member or as a client (role `Client`); `getWorkspace()` is the firm's own pages' version and answers null for a client too, so a page that is not open to clients stays closed to them (404); `getOpenWorkspaces()` lists every one the person can open, `getMyWorkspaces()` only the ones they work in and `getClientWorkspaces()` only the ones they are a client of.
