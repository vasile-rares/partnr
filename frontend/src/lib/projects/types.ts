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
};
