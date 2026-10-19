import "server-only";

import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { ProjectFile } from "@/lib/files/types";

// The workspace and project ids must already be checked (via `getWorkspace` and `getProject`).

/** Newest first, archived ones included: the owner restores them from the same list. */
export async function getProjectFiles(workspaceId: string, projectId: string): Promise<ProjectFile[]> {
  const response = await serverFetch(`/api/workspaces/${workspaceId}/projects/${projectId}/files?includeArchived=true`);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the files (HTTP ${response.status}).`);
  }

  return (await response.json()) as ProjectFile[];
}
