import "server-only";

import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { Member, MemberInvitation } from "@/lib/team/types";

// The workspace id must already be checked (via `getWorkspace`), which also rejects ids that are not UUIDs.

async function fetchList<T>(path: string, what: string): Promise<T[]> {
  const response = await serverFetch(path);

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load ${what} (HTTP ${response.status}).`);
  }

  return (await response.json()) as T[];
}

/** The team, the owner first. */
export const getMembers = (workspaceId: string) => fetchList<Member>(`/api/workspaces/${workspaceId}/members`, "the team");

/** The open invitations, newest first. Only the owner may ask: the API answers a member with 403. */
export const getMemberInvitations = (workspaceId: string) =>
  fetchList<MemberInvitation>(`/api/workspaces/${workspaceId}/member-invitations`, "the invitations");
