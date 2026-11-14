import "server-only";

import { serverFetch } from "@/lib/api/server";

export type PasswordResetLink =
  | { status: "usable"; email: string }
  | { status: "invalid" }
  /** Used, replaced by a newer link, or expired: the API says which. */
  | { status: "unusable"; message: string };

// The token is 256 random bits in URL-safe base64; anything else cannot be ours, and is never put into an API path.
const TOKEN = /^[A-Za-z0-9_-]{1,100}$/;

export async function getPasswordResetLink(token: string): Promise<PasswordResetLink> {
  if (!TOKEN.test(token)) {
    return { status: "invalid" };
  }

  const response = await serverFetch(`/api/auth/reset-password/${token}`);
  if (response.status === 404) {
    return { status: "invalid" };
  }
  if (response.status === 409) {
    const problem = (await response.json()) as { detail?: string };
    return { status: "unusable", message: problem.detail ?? "This link can no longer be used." };
  }
  if (!response.ok) {
    throw new Error(`Could not check the password reset link (HTTP ${response.status}).`);
  }

  return { status: "usable", email: ((await response.json()) as { email: string }).email };
}
