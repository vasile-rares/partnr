import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { hasSessionCookie, serverFetch } from "@/lib/api/server";

export type CurrentUser = {
  id: string;
  email: string;
  fullName: string;
  /** Optional profile details, null when not set. */
  phone: string | null;
  company: string | null;
};

/**
 * The signed-in user, or null. The cookie alone proves nothing (it may be expired or revoked), so the API is asked.
 * Wrapped in `cache` so any number of calls during one request cost a single round trip.
 */
export const getCurrentUser = cache(async (): Promise<CurrentUser | null> => {
  if (!(await hasSessionCookie())) {
    return null;
  }

  const response = await serverFetch("/api/auth/me");

  if (response.status === 401) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Could not load the current user (HTTP ${response.status}).`);
  }

  return (await response.json()) as CurrentUser;
});

/**
 * Call this in every protected page, next to the data it shows. Layouts are not a safe place for it: they do not
 * re-render on client navigation and do not stop their child pages from rendering.
 */
export async function requireUser(): Promise<CurrentUser> {
  const user = await getCurrentUser();
  if (!user) {
    redirect("/login");
  }
  return user;
}
