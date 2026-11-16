import "server-only";

import { redirect } from "next/navigation";
import { serverFetch } from "@/lib/api/server";
import type { GoogleConnection } from "@/lib/google/types";

export async function getGoogleConnection(): Promise<GoogleConnection> {
  const response = await serverFetch("/api/integrations/google");

  if (response.status === 401) {
    redirect("/login");
  }
  if (!response.ok) {
    throw new Error(`Could not load the Google connection (HTTP ${response.status}).`);
  }

  return (await response.json()) as GoogleConnection;
}
