import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { Project } from "@/lib/projects/types";
import { isUuid } from "@/lib/uuid";

// The workspace id must already be checked (via `getWorkspace`), which also rejects ids that are not UUIDs. Project and
// client ids come straight from the URL, so they are checked here.

type ProjectFilter = { includeArchived?: boolean; clientId?: string };

export async function getProjects(workspaceId: string, { includeArchived = false, clientId }: ProjectFilter = {}): Promise<Project[]> {
  const query = new URLSearchParams();
  if (includeArchived) {
    query.set("includeArchived", "true");
  }
  if (clientId && isUuid(clientId)) {
    query.set("clientId", clientId);
  }
  const suffix = query.size > 0 ? `?${query}` : "";

  const response = await serverFetch(`/api/workspaces/${workspaceId}/projects${suffix}`);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the projects (HTTP ${response.status}).`);
  }

  return (await response.json()) as Project[];
}

export const getProject = cache(async (workspaceId: string, projectId: string): Promise<Project | null> => {
  if (!isUuid(projectId)) {
    return null;
  }

  const response = await serverFetch(`/api/workspaces/${workspaceId}/projects/${projectId}`);

  if (response.status === 404) {
    return null;
  }
  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the project (HTTP ${response.status}).`);
  }

  return (await response.json()) as Project;
});
