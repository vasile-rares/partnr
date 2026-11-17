import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import { isUuid } from "@/lib/uuid";
import type { ClientWorkspace, OpenWorkspace, Workspace } from "@/lib/workspaces/types";

/** Every workspace the person can open, as a member or as a client of it; `role` tells which. */
export const getOpenWorkspaces = cache(async (): Promise<OpenWorkspace[]> => {
  const response = await serverFetch("/api/workspaces");

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load your workspaces (HTTP ${response.status}).`);
  }

  return (await response.json()) as OpenWorkspace[];
});

/** The ones the person works in. */
export const getMyWorkspaces = cache(async (): Promise<Workspace[]> =>
  (await getOpenWorkspaces()).filter((workspace): workspace is Workspace => workspace.role !== "Client"),
);

/** The ones the person is a client of. */
export const getClientWorkspaces = cache(async (): Promise<ClientWorkspace[]> =>
  (await getOpenWorkspaces()).filter((workspace): workspace is ClientWorkspace => workspace.role === "Client"),
);

/** The workspace as the person can open it, a member or a client, or null when it does not exist or is neither (the API answers 404 for all). */
export const getOpenWorkspace = cache(async (id: string): Promise<OpenWorkspace | null> => {
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

  return (await response.json()) as OpenWorkspace;
});

/** The workspace for the firm's own pages: null for a client too, so a page that is not open to clients stays closed to them. */
export const getWorkspace = cache(async (id: string): Promise<Workspace | null> => {
  const workspace = await getOpenWorkspace(id);

  return workspace && workspace.role !== "Client" ? workspace : null;
});
