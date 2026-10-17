import "server-only";

import { cookies } from "next/headers";
import { SESSION_COOKIE } from "@/lib/auth/constants";
import { BACKEND_URL } from "@/lib/backend";

/** True when the visitor sent a session cookie (it may still be expired; only the API can tell). */
export async function hasSessionCookie(): Promise<boolean> {
  return (await cookies()).has(SESSION_COOKIE);
}

/** Calls the API from the server with the visitor's session cookie forwarded; nothing is cached. */
export async function serverFetch(path: string): Promise<Response> {
  const session = (await cookies()).get(SESSION_COOKIE);

  return fetch(`${BACKEND_URL}${path}`, {
    headers: session ? { cookie: `${session.name}=${session.value}` } : undefined,
    cache: "no-store",
  });
}
