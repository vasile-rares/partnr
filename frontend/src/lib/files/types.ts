/** Draft is only visible to the workspace; Shared is what the client sees. */
export type ProjectFileStatus = "Draft" | "Shared";

/** Same limit as the API's (`ProjectFile.MaxSizeBytes`), only used to refuse an oversized file before it is uploaded. */
export const MAX_FILE_BYTES = 25 * 1024 * 1024;

export type ProjectFile = {
  id: string;
  projectId: string;
  /** What people see in the lists; starts as the file name. */
  name: string;
  description: string | null;
  /** The uploaded file's own name, used for the download. */
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: ProjectFileStatus;
  createdAtUtc: string;
  /** Set when archived: hidden from the client, and read-only until restored. */
  archivedAtUtc: string | null;
};
