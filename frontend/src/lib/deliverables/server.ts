import "server-only";

import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { Deliverable } from "@/lib/deliverables/types";

// The workspace and project ids must already be checked (via `getWorkspace` and `getProject`).

/** Newest first, archived ones included: the owner restores them from the same list. */
export async function getDeliverables(workspaceId: string, projectId: string): Promise<Deliverable[]> {
  const response = await serverFetch(`/api/workspaces/${workspaceId}/projects/${projectId}/deliverables?includeArchived=true`);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the deliverables (HTTP ${response.status}).`);
  }

  return (await response.json()) as Deliverable[];
}
