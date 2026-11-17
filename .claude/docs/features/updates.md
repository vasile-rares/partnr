# Project updates (the timeline's written notes)

## API

- `/api/workspaces/{id}/projects/{projectId}/updates`, `?includeArchived=true`: their own aggregate (`ProjectUpdate`, `IProjectUpdateRepository`) with a `Title` and an optional plain-text `Body`.
- They follow the deliverables' rules exactly: `Draft` / `Shared` by hand, members create, edit, share and delete a draft (a shared one answers 409), archive and restore are owner-only and archiving sets the status back to `Draft`, an archived update and every update of an archived project are read-only (409) but can be read, another project's update is a 404.
- `SharedAtUtc` is set on the first share and kept; the list the freelancer reads is newest created first.
- The client sees only `Shared`, non-archived ones inside the client's answer to `GET /api/workspaces/{id}/projects/{projectId}` (`updates`, `ClientProjectUpdateResponse`: id, title, body and the share date only, most recently shared first; `IProjectUpdateRepository.ListSharedAsync` runs only after the project itself was found as shared with the client).
- There is no automatic event log; see the plan's ideas.

## Web app

- The "Updates" section on the project page (`src/components/updates/`, data in `src/lib/updates/`). Same shape as deliverables: `getProjectUpdates()` loads archived ones too, the owner restores them from the same list.
