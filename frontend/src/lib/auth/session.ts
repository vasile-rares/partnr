import "server-only";

import { cache } from "react";
import { redirect } from "next/navigation";
import { hasSessionCookie, serverFetch } from "@/lib/api/server";

export type CurrentUser = {
  id: string;
  email: string;
  fullName: string;
  phone: string | null;
  company: string | null;
};

/** The signed-in user, or null. The cookie alone proves nothing, so the API is asked; `cache` makes it one round trip per request. */
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

/** Call this in every protected page: layouts do not re-render on client navigation nor stop their child pages from rendering. */
export async function requireUser(): Promise<CurrentUser> {
  const user = await getCurrentUser();
  if (!user) {
    redirect("/login");
  }
  return user;
}
