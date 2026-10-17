import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { Client } from "@/lib/clients/types";
import { isUuid } from "@/lib/uuid";

// The workspace id in both functions must already be checked (via `getWorkspace`), which also rejects ids that are
// not UUIDs. The client id comes straight from the URL, so it is checked here.

export async function getClients(workspaceId: string, includeArchived = false): Promise<Client[]> {
  const query = includeArchived ? "?includeArchived=true" : "";
  const response = await serverFetch(`/api/workspaces/${workspaceId}/clients${query}`);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the clients (HTTP ${response.status}).`);
  }

  return (await response.json()) as Client[];
}

export const getClient = cache(async (workspaceId: string, clientId: string): Promise<Client | null> => {
  if (!isUuid(clientId)) {
    return null;
  }

  const response = await serverFetch(`/api/workspaces/${workspaceId}/clients/${clientId}`);

  if (response.status === 404) {
    return null;
  }
  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the client (HTTP ${response.status}).`);
  }

  return (await response.json()) as Client;
});
