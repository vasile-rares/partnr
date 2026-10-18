/** Draft is only visible to the workspace; Shared is what the client sees. */
export type ProjectUpdateStatus = "Draft" | "Shared";

export type ProjectUpdate = {
  id: string;
  projectId: string;
  title: string;
  /** Plain text with line breaks. */
  body: string | null;
  status: ProjectUpdateStatus;
  createdAtUtc: string;
  /** Set on the first share and kept afterwards; the client sees this date. */
  sharedAtUtc: string | null;
  /** Set when archived: hidden from the client, and read-only until restored. */
  archivedAtUtc: string | null;
};
