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
  stages: Stage[];
};
