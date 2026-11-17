import type { ClientDeliverable } from "@/lib/deliverables/types";
import type { ClientFile } from "@/lib/files/types";
import type { ClientUpdate } from "@/lib/updates/types";

/** Set by the workspace, never automatically. Planning is a draft the client does not see yet. */
export type ProjectStatus = "Planning" | "Active" | "OnHold" | "Completed";

export const projectStatuses: { value: ProjectStatus; label: string }[] = [
  { value: "Planning", label: "Planning" },
  { value: "Active", label: "Active" },
  { value: "OnHold", label: "On hold" },
  { value: "Completed", label: "Completed" },
];

export const statusLabel = (status: ProjectStatus): string =>
  projectStatuses.find((s) => s.value === status)?.label ?? status;

/** Several stages can be in progress at once. */
export type StageStatus = "ToDo" | "InProgress" | "Done";

export const stageStatuses: { value: StageStatus; label: string }[] = [
  { value: "ToDo", label: "To do" },
  { value: "InProgress", label: "In progress" },
  { value: "Done", label: "Done" },
];

export const stageStatusLabel = (status: StageStatus): string =>
  stageStatuses.find((s) => s.value === status)?.label ?? status;

export type Stage = {
  id: string;
  name: string;
  description: string | null;
  dueDate: string | null;
  status: StageStatus;
};

export type Progress = { done: number; total: number };

/** Counted by deliverable: the ones waiting for the client's answer, and the ones the client sent back with changes. */
export type ProjectApprovals = { pending: number; changesRequested: number };

export type Project = {
  id: string;
  client: { id: string; displayName: string };
  name: string;
  description: string | null;
  startDate: string | null;
  dueDate: string | null;
  status: ProjectStatus;
  createdAtUtc: string;
  /** Set when the project is archived: hidden from the default list, and read-only until restored. */
  archivedAtUtc: string | null;
  progress: Progress;
  approvals: ProjectApprovals;
  stages: Stage[];
};

/** What a client sees of a project in a list: no client card, no notes, never Planning. */
export type ClientProjectSummary = {
  id: string;
  workspace: { id: string; name: string };
  name: string;
  status: ProjectStatus;
  dueDate: string | null;
  progress: Progress;
  /** How many deliverables wait for this client's answer. */
  awaitingApproval: number;
};

/** One project as the client's account gets it: only what was shared with them. */
export type ClientProject = ClientProjectSummary & {
  description: string | null;
  startDate: string | null;
  stages: Stage[];
  deliverables: ClientDeliverable[];
  updates: ClientUpdate[];
  files: ClientFile[];
};
