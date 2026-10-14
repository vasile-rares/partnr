import type { Progress, Stage, ProjectStatus } from "@/lib/projects/types";

/** A workspace the signed-in user is a client of. */
export type PortalWorkspace = {
  id: string;
  name: string;
};

/**
 * A project in the client's list. What the portal shows of a project is deliberately smaller than what the workspace sees
 * (`Project`): no client card, no notes. The status is never "Planning": a draft is not shared with the client.
 */
export type PortalProjectSummary = {
  id: string;
  workspace: PortalWorkspace;
  name: string;
  status: ProjectStatus;
  /** A calendar date, "YYYY-MM-DD". */
  dueDate: string | null;
  progress: Progress;
};

/** A shared project with everything its page shows. */
export type PortalProject = PortalProjectSummary & {
  description: string | null;
  startDate: string | null;
  /** In order, first to last. */
  stages: Stage[];
};
