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
};

export type PortalProject = PortalProjectSummary & {
  description: string | null;
  startDate: string | null;
  stages: Stage[];
};
