import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { PortalProject, PortalProjectSummary, PortalWorkspace } from "@/lib/portal/types";
import { isUuid } from "@/lib/uuid";

export const getPortalWorkspaces = cache(async (): Promise<PortalWorkspace[]> => {
  const response = await serverFetch("/api/portal/workspaces");

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load your portal (HTTP ${response.status}).`);
  }

  return (await response.json()) as PortalWorkspace[];
});

export const getPortalProjects = cache(async (): Promise<PortalProjectSummary[]> => {
  const response = await serverFetch("/api/portal/projects");

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load your projects (HTTP ${response.status}).`);
  }

  return (await response.json()) as PortalProjectSummary[];
});

/** One shared project, or null when there is none like it for this client (a draft and someone else's look the same). */
export const getPortalProject = cache(async (projectId: string): Promise<PortalProject | null> => {
  // The id comes straight from the URL, so it is checked before it goes into a URL to the API.
  if (!isUuid(projectId)) {
    return null;
  }

  const response = await serverFetch(`/api/portal/projects/${projectId}`);

  if (response.status === 404) {
    return null;
  }
  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the project (HTTP ${response.status}).`);
  }

  return (await response.json()) as PortalProject;
});
