import { VideoIcon } from "lucide-react";
import Link from "next/link";
import { MeetingTime } from "@/components/meetings/MeetingTime";
import { buttonVariants } from "@/components/ui/button";
import type { PortalMeeting } from "@/lib/meetings/types";

/** The next call with the firm, on the client's home page; nothing when none is planned. */
export function PortalNextMeeting({ workspaceId, meeting }: { workspaceId: string; meeting: PortalMeeting | null }) {
  if (!meeting) {
    return null;
  }

  return (
    <section aria-labelledby="next-meeting-title" className="portal-next-meeting">
      <h2 id="next-meeting-title" className="mb-4 font-heading text-xl font-semibold">
        Next meeting
      </h2>
      <div className="portal-next-meeting-card flex flex-wrap items-center justify-between gap-4 rounded-lg border p-4">
        <div className="min-w-0">
          <p className="font-medium">{meeting.title}</p>
          <MeetingTime startsAtUtc={meeting.startsAtUtc} endsAtUtc={meeting.endsAtUtc} className="mt-1 block text-sm" />
          {meeting.project ? <p className="mt-1 text-sm text-muted-foreground">{meeting.project.name}</p> : null}
        </div>
        <div className="flex items-center gap-3">
          <Link href={`/w/${workspaceId}/meetings`} className="text-sm text-muted-foreground underline underline-offset-4 hover:text-foreground">
            All meetings
          </Link>
          {meeting.meetUrl ? (
            <a href={meeting.meetUrl} target="_blank" rel="noopener noreferrer" className={buttonVariants({ size: "sm" })}>
              <VideoIcon data-icon="inline-start" />
              Join
              <span className="sr-only"> {meeting.title} on Google Meet</span>
            </a>
          ) : null}
        </div>
      </div>
    </section>
  );
}
