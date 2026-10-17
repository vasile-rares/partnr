import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import { isUuid } from "@/lib/uuid";
import type { Workspace } from "@/lib/workspaces/types";

export const getMyWorkspaces = cache(async (): Promise<Workspace[]> => {
  const response = await serverFetch("/api/workspaces");

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load your workspaces (HTTP ${response.status}).`);
  }

  return (await response.json()) as Workspace[];
});

/** The workspace, or null when it does not exist or the user is not a member (the API answers 404 for both). */
export const getWorkspace = cache(async (id: string): Promise<Workspace | null> => {
  if (!isUuid(id)) {
    return null;
  }

  const response = await serverFetch(`/api/workspaces/${id}`);

  if (response.status === 404) {
    return null;
  }
  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the workspace (HTTP ${response.status}).`);
  }

  return (await response.json()) as Workspace;
});
