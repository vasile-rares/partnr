import { VideoIcon } from "lucide-react";
import Link from "next/link";
import { MeetingTime } from "@/components/meetings/MeetingTime";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { durationLabel, durationOf } from "@/lib/meetings/time";
import type { PortalMeeting } from "@/lib/meetings/types";

type PortalMeetingListProps = {
  workspaceId: string;
  meetings: PortalMeeting[];
  /** Said when the list is empty. */
  empty: string;
  /** Upcoming ones can be joined; history cannot. */
  joinable?: boolean;
};

/** The client's meetings with one firm: when, about what, and a button into the call. Reading only: changes come from the firm, through Google. */
export function PortalMeetingList({ workspaceId, meetings, empty, joinable = false }: PortalMeetingListProps) {
  if (meetings.length === 0) {
    return <p className="portal-meeting-list-empty rounded-lg border border-dashed p-6 text-sm text-muted-foreground">{empty}</p>;
  }

  return (
    <ul className="portal-meeting-list flex flex-col divide-y rounded-lg border">
      {meetings.map((meeting) => (
        <li key={meeting.id} className="portal-meeting-item flex flex-wrap items-start justify-between gap-x-4 gap-y-3 p-4">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <p className="font-medium">{meeting.title}</p>
              {meeting.status === "Cancelled" ? <Badge variant="outline">Cancelled</Badge> : null}
            </div>
            <MeetingTime startsAtUtc={meeting.startsAtUtc} endsAtUtc={meeting.endsAtUtc} className="mt-1 block text-sm" />
            <p className="mt-1 text-sm text-muted-foreground">
              {meeting.project ? (
                <>
                  <Link href={`/w/${workspaceId}/projects/${meeting.project.id}`} className="hover:text-foreground hover:underline">
                    {meeting.project.name}
                  </Link>
                  {" · "}
                </>
              ) : null}
              {durationLabel(durationOf(meeting))}
            </p>
            {meeting.description ? <p className="mt-2 whitespace-pre-line text-sm text-muted-foreground">{meeting.description}</p> : null}
          </div>
          {joinable && meeting.meetUrl && meeting.status === "Scheduled" ? (
            <a href={meeting.meetUrl} target="_blank" rel="noopener noreferrer" className={buttonVariants({ size: "sm" })}>
              <VideoIcon data-icon="inline-start" />
              Join
              <span className="sr-only"> {meeting.title} on Google Meet</span>
            </a>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
