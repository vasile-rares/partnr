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

/** Only shared ones reach the portal, so there is no status here. Newest first. */
export type PortalDeliverable = {
  id: string;
  title: string;
  description: string | null;
  /** Always an https address (the API refuses anything else). */
  url: string;
  stageName: string | null;
};

export type PortalProject = PortalProjectSummary & {
  description: string | null;
  startDate: string | null;
  stages: Stage[];
  deliverables: PortalDeliverable[];
};
