# Clients

Inviting a client and the locked details: [invitations.md](invitations.md). The client's own portal: [portal.md](portal.md). Decisions: `decisions.md`, "Clienți".

## API

- `/api/workspaces/{id}/clients` (`ClientService`): `POST`, `GET ?includeArchived=true`, `GET {id}`, `PUT {id}`, `POST {id}/archive|restore`, plus the invitation routes and the suggestions below.
- Any member adds and edits clients and sees all of them; **archiving and restoring is owner-only** (`RequireOwnerAsync`). Archiving also cancels the client's open invitation.
- The email is unique inside a workspace (409 "A client with this email already exists in this workspace."); it is stored normalised (`Client.NormalizeEmail`).
- A client card is a record of the firm's, not an account: it gets one only by accepting an invitation, which links it (`Client.UserId`). The notes are the firm's own and never reach the client.
- **Client name is optional** (`Client.Name`, `ClientResponse.Name` are nullable): a card can be just an email. UIs show `name ?? email` until the client has an account, whose name replaces it.
- **Account suggestions** (`GET /api/workspaces/{id}/clients/suggestions?email=`, `ClientSuggestionService`) are a directory of accounts, so they are narrow on purpose: members of the workspace only (404 otherwise), at least 3 characters (an empty list below that), at most 5 results, a match on the start of the email only (never the name), workspace members left out. The endpoint has its own **rate limit** (`RateLimitPolicies.Suggestions`, 60 a minute per signed-in user, answering 429), defined in `Program.cs` with `AddRateLimiter`/`UseRateLimiter` (placed after authentication so it can follow the user). New endpoints that look people up should reuse that policy or add one next to it.

## Web app

- `src/app/w/[workspaceId]/clients/` and `src/components/clients/`, data in `src/lib/clients/`.
- A client is added in `AddClientDialog` / `AddClientForm`: from an email, with existing accounts offered while typing (the suggestions endpoint, asked a moment after the typing stops; choosing one links nothing), and invited in the same go ([invitations.md](invitations.md)).
- The list is `ClientsTable` (client: search and the Active / Archived tabs filter what the page already loaded, the scope is in `?archived=1`; each row has an avatar, the open projects count computed in the page from `getProjects`, and the portal status). The rows are drawn by the ReUI data grid (`src/components/reui/data-grid/`, vendored from the `@reui` registry in `components.json`, TanStack Table v9): sortable column headers and, past 10 rows, pagination. It is the first screen to use it, as a trial; other tables still use `ui/table`.
- A client's page has a profile header (`ClientAvatar`, status, mailto / tel links), the client's projects (the same `ProjectsTable` as the projects list), and in the right column `ClientPortalCard` (one short status line and the invitation buttons; the long explanation is an `InfoTip`, and "when this client was added" is another one next to the status badge) and `ClientNotesCard`; editing and archiving are in the "Edit client" `Sheet`.
