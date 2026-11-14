import "server-only";

import { serverFetch } from "@/lib/api/server";
import type { Conversation, ConversationSummary } from "./types";

/** The conversations the signed-in person can read on this side, latest first. */
export async function getConversations(apiBase: string): Promise<ConversationSummary[]> {
  const response = await serverFetch(apiBase);
  if (!response.ok) {
    throw new Error(`Could not load the conversations (HTTP ${response.status}).`);
  }

  return (await response.json()) as ConversationSummary[];
}

/** Null when there is no such conversation for this person (the project is not theirs, or the client cannot open it). */
export async function getConversation(apiBase: string, projectId: string): Promise<Conversation | null> {
  const response = await serverFetch(`${apiBase}/${encodeURIComponent(projectId)}`);
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Could not load the conversation (HTTP ${response.status}).`);
  }

  return (await response.json()) as Conversation;
}
