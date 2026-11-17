# The client's side: invitations and the portal

Who may open what through `/api/portal/...` is an authorization rule, so it stays in `backend/CLAUDE.md` ("Authorization").

## Web app

- `/invite/[token]` (in `src/app/(auth)/`, public, listed in the `proxy.ts` matcher exclusions) opens an invitation.
- A client's portal is per firm: `/portal` only redirects (to the first firm they are a client of, or explains there is none yet).
- `/portal/[workspaceId]` is that firm's home: a welcome line, "Waiting for you" with a link per deliverable to the project's Deliverables tab, "Active projects" with `summarize`'s Right now / Up next, and "Latest updates" across the firm's projects; it loads each project with `getPortalProject`, in parallel.
- `/portal/[workspaceId]/projects` lists its projects (`PortalProjectList`; `getPortalWorkspace()`, `getPortalProjects()` in `src/lib/portal/server.ts`; the layout `app/portal/[workspaceId]/layout.tsx` answers 404 for a firm the person is not a client of).
- `/portal/[workspaceId]/projects/[projectId]` is one project (404 under any firm but its own), on `ProjectTabs` (shared with the freelancer's page; it takes the list of tabs): Overview (description, progress, the three latest updates, stages), Deliverables (hidden while none is shared; its tab badge counts the ones waiting for the client), Updates (the five latest open, the rest in a `<details>`) and Files (hidden while none is shared; `PortalFileList`, download links to `/api/portal/projects/{id}/files/{fileId}/download`). In e2e tests click the tab first (`openTab(page, "Updates")`).
- A project that waits for the client's answer shows a "Waiting for you" banner on Overview (`awaitingApproval`, its button opens Deliverables), and the `/portal` list a "N waiting for you" badge.
- Each deliverable carries `PortalApprovalPanel` (`src/components/portal/`): the freelancer's note, "Approve" (behind a confirmation, the answer is final) and "Request changes" (a message is required), and the earlier answers in a `<details>` (`getPortalProject()`, wording in `src/lib/portal/summary.ts`).
