import Link from "next/link";
import { Badge } from "@/components/reui/badge";
import type { Meeting } from "@/lib/meetings/types";

/** Where the event stands in Google, for a meeting that is not in step with it. */
export function SyncBadge({ meeting }: { meeting: Meeting }) {
  if (meeting.syncStatus === "Failed") {
    return <Badge variant="destructive-light">Couldn&apos;t reach Google</Badge>;
  }
  if (meeting.syncStatus === "NeedsReconnect") {
    return (
      <Link href="/account" className="meeting-reconnect">
        <Badge variant="destructive-light">Reconnect Google</Badge>
      </Link>
    );
  }

  return <Badge variant="secondary">{meeting.status === "Cancelled" ? "Cancelling in Google…" : "Sending to Google…"}</Badge>;
}
