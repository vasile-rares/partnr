import type { Progress, Stage, ProjectStatus } from "@/lib/projects/types";

export type PortalWorkspace = {
  id: string;
  name: string;
};

/** What the portal shows of a project is smaller than `Project`: no client card, no notes. Never Planning. */
export type PortalProjectSummary = {
  id: string;
  workspace: PortalWorkspace;
  name: string;
  status: ProjectStatus;
  dueDate: string | null;
  progress: Progress;
  /** How many deliverables wait for this client's answer. */
  awaitingApproval: number;
};

/** What the client was asked, and what they answered. Rounds the freelancer withdrew never reach the portal. */
export type PortalApproval = {
  status: "None" | "Pending" | "Approved" | "ChangesRequested";
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

/** Only shared ones reach the portal, so there is no status here. Newest first. */
export type PortalDeliverable = {
  id: string;
  title: string;
  description: string | null;
  /** Always an https address (the API refuses anything else). */
  url: string;
  stageName: string | null;
  approval: PortalApproval;
};

/** Only shared ones reach the portal. Most recently shared first; no author on purpose. */
export type PortalProjectUpdate = {
  id: string;
  title: string;
  /** Plain text with line breaks. */
  body: string | null;
  /** The date the client sees. */
  sharedAtUtc: string;
};

/** Only shared ones reach the portal. Newest first; downloaded through `/api/portal/projects/{id}/files/{fileId}/download`. */
export type PortalProjectFile = {
  id: string;
  name: string;
  description: string | null;
  /** The file's own name, which the download is saved as. */
  fileName: string;
  sizeBytes: number;
};

export type PortalProject = PortalProjectSummary & {
  description: string | null;
  startDate: string | null;
  stages: Stage[];
  deliverables: PortalDeliverable[];
  updates: PortalProjectUpdate[];
  files: PortalProjectFile[];
};
