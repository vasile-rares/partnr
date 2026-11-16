"use client";

import { useTimeZone } from "@/components/meetings/useInBrowser";
import { whenLabel } from "@/lib/meetings/time";

/** When a meeting is, in the reader's own time zone: "Fri 20 Nov · 09:30–10:00 EET". */
export function MeetingTime({ startsAtUtc, endsAtUtc, className }: { startsAtUtc: string; endsAtUtc: string; className?: string }) {
  const timeZone = useTimeZone();

  return (
    <time dateTime={startsAtUtc} className={className}>
      {whenLabel({ startsAtUtc, endsAtUtc }, timeZone)}
    </time>
  );
}
