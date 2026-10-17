import "server-only";

import { cache } from "react";
import { serverFetch } from "@/lib/api/server";

export type InvitationPreview = {
  workspaceName: string;
  email: string;
  status: "Pending" | "Expired" | "Cancelled" | "Accepted";
  accountExists: boolean;
  expiresAtUtc: string;
};

// The token is 256 random bits in URL-safe base64, so anything else cannot be ours. It is rejected before it reaches
// a URL, so a crafted segment such as ".." cannot turn /api/invitations/{token} into a different API path.
const TOKEN = /^[A-Za-z0-9_-]{1,100}$/;

/** The invitation, or null when the link is not valid (the API answers 404 for every kind of unknown token). */
export const getInvitation = cache(async (token: string): Promise<InvitationPreview | null> => {
  if (!TOKEN.test(token)) {
    return null;
  }

  const response = await serverFetch(`/api/invitations/${token}`);

  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Could not load the invitation (HTTP ${response.status}).`);
  }

  return (await response.json()) as InvitationPreview;
});
