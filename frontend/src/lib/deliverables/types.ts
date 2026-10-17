/** Draft is only visible to the workspace; Shared is what the client sees. */
export type DeliverableStatus = "Draft" | "Shared";

export type Deliverable = {
  id: string;
  projectId: string;
  stageId: string | null;
  /** Null when the deliverable has no stage, or its stage was deleted. */
  stageName: string | null;
  title: string;
  description: string | null;
  /** Always an https address (the API refuses anything else). */
  url: string;
  status: DeliverableStatus;
  createdAtUtc: string;
  /** Set when archived: hidden from the client, and read-only until restored. */
  archivedAtUtc: string | null;
};
