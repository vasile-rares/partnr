/** Draft is only visible to the workspace; Shared is what the client sees. */
export type DeliverableStatus = "Draft" | "Shared";

/** One round of asking the client to sign off. A closed round (anything but Pending) never changes again. */
export type ApprovalRoundStatus = "Pending" | "Approved" | "ChangesRequested" | "Withdrawn";

/** What a deliverable shows: the latest round that was not withdrawn, or None when nothing was asked. */
export type ApprovalStatus = "None" | Exclude<ApprovalRoundStatus, "Withdrawn">;

export type ApprovalRound = {
  id: string;
  status: ApprovalRoundStatus;
  /** What the freelancer asked the client to look at. */
  note: string | null;
  requestedAtUtc: string;
  /** The client's words: optional on approval, always there when changes were requested. */
  message: string | null;
  /** When the round closed, by a decision or a withdrawal. */
  resolvedAtUtc: string | null;
};

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
  approvalStatus: ApprovalStatus;
  /** Every round, newest first, withdrawn ones included. */
  approvals: ApprovalRound[];
  createdAtUtc: string;
  /** Set when archived: hidden from the client, and read-only until restored. */
  archivedAtUtc: string | null;
};

/** What the client was asked, and what they answered. Rounds the freelancer withdrew never reach them. */
export type ClientApproval = {
  status: ApprovalStatus;
  /** The round waiting for an answer, if any. */
  pending: { id: string; note: string | null; requestedAtUtc: string } | null;
  /** Answered rounds, newest first. */
  history: {
    decision: "Approved" | "ChangesRequested";
    note: string | null;
    message: string | null;
    requestedAtUtc: string;
    decidedAtUtc: string;
  }[];
};

/** Only shared ones reach the client, so there is no status here. Newest first. */
export type ClientDeliverable = {
  id: string;
  title: string;
  description: string | null;
  /** Always an https address (the API refuses anything else). */
  url: string;
  stageName: string | null;
  approval: ClientApproval;
};
