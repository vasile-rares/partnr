/**
 * Where a project stands, set by the workspace (never automatically):
 * - Planning: a draft the client does not see yet;
 * - Active: work is under way;
 * - OnHold: paused, for example while waiting on the client;
 * - Completed: delivered.
 */
export type ProjectStatus = "Planning" | "Active" | "OnHold" | "Completed";

export const projectStatuses: { value: ProjectStatus; label: string }[] = [
  { value: "Planning", label: "Planning" },
  { value: "Active", label: "Active" },
  { value: "OnHold", label: "On hold" },
  { value: "Completed", label: "Completed" },
];

export const statusLabel = (status: ProjectStatus): string =>
  projectStatuses.find((s) => s.value === status)?.label ?? status;

/** Where one stage of a project stands. Several stages can be in progress at once. */
export type StageStatus = "ToDo" | "InProgress" | "Done";

export const stageStatuses: { value: StageStatus; label: string }[] = [
  { value: "ToDo", label: "To do" },
  { value: "InProgress", label: "In progress" },
  { value: "Done", label: "Done" },
];

export const stageStatusLabel = (status: StageStatus): string =>
  stageStatuses.find((s) => s.value === status)?.label ?? status;

/** One step of a project, such as Design or Launch. The API returns a project's stages in order. */
export type Stage = {
  id: string;
  name: string;
  description: string | null;
  /** A calendar date, "YYYY-MM-DD". */
  dueDate: string | null;
  status: StageStatus;
};

/** How many of the project's stages are done. */
export type Progress = { done: number; total: number };

/** A project as the API returns it. Optional details are null when not set. */
export type Project = {
  id: string;
  /** The client is fixed when the project is created. */
  client: { id: string; displayName: string };
  name: string;
  description: string | null;
  /** Calendar dates, "YYYY-MM-DD". */
  startDate: string | null;
  dueDate: string | null;
  status: ProjectStatus;
  createdAtUtc: string;
  /** Set when the project is archived: hidden from the default list, and read-only until restored. */
  archivedAtUtc: string | null;
  progress: Progress;
  stages: Stage[];
};
