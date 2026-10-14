import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";

/** A workspace the signed-in user is a client of. */
export type PortalWorkspace = {
  id: string;
  name: string;
};

/** The workspaces the signed-in user is a client of (not a member: those are in `getMyWorkspaces`), oldest first. */
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
