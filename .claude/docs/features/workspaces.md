# Workspaces

Who may touch a workspace (`WorkspaceAccess`, 404 for a non-member, 403 for a member doing an owner-only action) is in `backend/CLAUDE.md` ("Authorization"). Decisions: `decisions.md`, "Workspace".

## API

- `/api/workspaces` (`WorkspaceService`): `POST` creates one with the caller as its `Owner`, `GET` lists the caller's own (each with the caller's `role`), `GET {id}` answers for a member only, `PATCH {id}` renames it and is **owner-only**.
- The name is required and at most 100 characters (`Workspace.NameMaxLength`, checked in the entity).
- Members are `WorkspaceMember` rows inside the `Workspace` aggregate, with a role (`Owner`, `Member`). There is no endpoint to add or remove members yet; the integration tests and the seed add them straight to the database.
- A member of a workspace cannot also be its client (accepting an invitation answers 409, see [invitations.md](invitations.md)).

## Web app

- `/` only redirects: no workspace → `/workspaces/new`, one → `/w/{id}`, several → `/workspaces`; someone with no workspace of their own but a client link → `/portal`.
- `/workspaces` (`app/(shell)/workspaces/page.tsx`) lists the caller's workspaces with their role and links to each; `/workspaces/new` holds `CreateWorkspaceForm`. Both sit in the shell with `kind: "none"` ([app-shell.md](app-shell.md)).
- `/w/[workspaceId]/settings`: the owner renames the workspace (`RenameWorkspaceForm`); a member only sees the name and is told only the owner can rename it.
- Data in `src/lib/workspaces/` (`getWorkspace()` answers null for a workspace the person is not a member of, `getMyWorkspaces()`).
