"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import type { Meeting } from "@/lib/meetings/types";

const SYNC_REFRESH_MS = 4000;

/** The background sync answers within seconds, so the page asks again until nothing is waiting: the Meet link appears on its own. `now` is what the server thinks it is. */
export function useRefreshWhileSyncing(meetings: Meeting[], now: string) {
  const router = useRouter();
  const waiting = meetings.some((meeting) => meeting.syncStatus === "Pending" && Date.parse(meeting.endsAtUtc) > Date.parse(now));

  useEffect(() => {
    if (!waiting) {
      return;
    }
    const timer = setInterval(() => router.refresh(), SYNC_REFRESH_MS);
    return () => clearInterval(timer);
  }, [waiting, router]);
}
