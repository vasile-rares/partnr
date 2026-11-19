# Project files

The bytes belong to the **Storage service** (`backend/src/Services/Storage`, database `partnr_storage`, gRPC port 5057); this service keeps the rules and the rows. Its setup (RustFS / S3) is in `backend/CLAUDE.md`.

## API

- `/api/workspaces/{id}/projects/{projectId}/files`, `?includeArchived=true`: their own aggregate (`ProjectFile`, `IProjectFileRepository`, `ProjectFileService`), separate from deliverables and with no approvals.
- `POST` takes multipart (`file`, optional `name` and `description`); the bytes go to `IFileStorage` (gRPC to Storage, in chunks) under a key made of ids only (`{workspaceId}/{projectId}/{fileId}`), written **before** the row; if the row then fails to save the bytes are deleted again (best effort). Delete removes the row and publishes `ProjectFileRemoved` in the same transaction, and Storage deletes the bytes, retrying until it works, so an orphan can only come from the failed-upload case. If Storage cannot be reached, an upload or a download answers 503.
- The entity enforces the rules (`DomainException`, 400): at most 25 MB, not empty, an allow-list of extensions (checked by extension only; the served content type comes from it, never from the browser).
- `GET {id}/download` is always an `attachment` with `nosniff` and `Content-Length` (`ControllerBaseExtensions.DownloadFile`).
- Storage keeps a record per key (size, content type, SHA-256) and refuses more than 25 MB itself too; it never decides access: every route above goes through this service first.
- It follows the deliverables' rules otherwise: `Draft` / `Shared` by hand, members upload, edit (name and description; the file is never replaced), share and delete a draft (a shared one answers 409), archive and restore are owner-only and archiving sets the status back to `Draft`, an archived file and every file of an archived project are read-only (409) but can be read and downloaded, another project's file is a 404.
- The client sees only `Shared`, non-archived ones inside the client's answer to `GET /api/workspaces/{id}/projects/{projectId}` (`files`, `ClientProjectFileResponse`: id, name, description, fileName and sizeBytes only; `IProjectFileRepository.ListSharedAsync` runs only after the project was found as shared with the client) and downloads through the same `GET /api/workspaces/{id}/projects/{projectId}/files/{fileId}/download` as the firm (`ProjectFileService.DownloadAsync`, which answers a client for a shared, non-archived file of a project shared with them; a draft, retracted or archived file, a project in Planning or archived and someone else's project all answer 404).

## Web app

- The "Files" section on the project page (`src/components/files/FirmFileList.tsx`, data in `src/lib/files/`): upload (a drop area is always at the top of the section, the one exception to "adding is asked for": click to browse or drop a file on it, or anywhere on the section, and a dialog opens with that file chosen, where it is named and given a note, 25 MB; the dialog's own `FileDropzone` swaps the file; the area is hidden on an archived project), download, Share / Unshare, Edit (name and description, the file itself is never replaced), Archive / Restore (owner) and Delete (a draft only).
- `getProjectFiles()` loads archived ones too.
- Uploads go through `sendForm` (`src/lib/api/client.ts`); `next.config.ts` raises `proxyClientMaxBodySize` because Next cuts a proxied body at 10 MB. Downloads are plain `<a download>` links to the API (it answers with an attachment).
