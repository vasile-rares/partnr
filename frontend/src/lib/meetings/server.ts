import "server-only";

import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { Meeting, ClientMeeting } from "@/lib/meetings/types";
import { isUuid } from "@/lib/uuid";

// The workspace id must already be checked (via `getWorkspace` / `getOpenWorkspace`), which also rejects ids that are not UUIDs.

/** Cancelled ones too: they are history, shown under "Past". */
export async function getMeetings(workspaceId: string, clientId?: string): Promise<Meeting[]> {
  const query = new URLSearchParams({ includeCancelled: "true" });
  if (clientId && isUuid(clientId)) {
    query.set("clientId", clientId);
  }
  const response = await serverFetch(`/api/workspaces/${workspaceId}/meetings?${query}`);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the meetings (HTTP ${response.status}).`);
  }

  return (await response.json()) as Meeting[];
}

export async function getClientMeetings(workspaceId: string): Promise<ClientMeeting[]> {
  // The same route as the firm's: a client gets their own meetings, in their own shape.
  const response = await serverFetch(`/api/workspaces/${workspaceId}/meetings`);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the meetings (HTTP ${response.status}).`);
  }

  return (await response.json()) as ClientMeeting[];
}
