import "server-only";

import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { ProjectUpdate } from "@/lib/updates/types";

// The workspace and project ids must already be checked (via `getWorkspace` and `getProject`).

/** Newest first, archived ones included: the owner restores them from the same list. */
export async function getProjectUpdates(workspaceId: string, projectId: string): Promise<ProjectUpdate[]> {
  const response = await serverFetch(`/api/workspaces/${workspaceId}/projects/${projectId}/updates?includeArchived=true`);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the updates (HTTP ${response.status}).`);
  }

  return (await response.json()) as ProjectUpdate[];
}
