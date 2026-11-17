import Link from "next/link";
import { DividedList } from "@/components/layout/DividedList";
import { JoinMeetingButton } from "@/components/meetings/JoinMeetingButton";
import { MeetingTime } from "@/components/meetings/MeetingTime";
import { Badge } from "@/components/ui/badge";
import { durationLabel, durationOf } from "@/lib/meetings/time";
import type { ClientMeeting } from "@/lib/meetings/types";

type ClientMeetingListProps = {
  workspaceId: string;
  meetings: ClientMeeting[];
  /** Said when the list is empty. */
  empty: string;
  /** Upcoming ones can be joined; history cannot. */
  joinable?: boolean;
};

/** The client's meetings with one workspace: when, about what, and a button into the call. Reading only: changes come from the members, through Google. */
export function ClientMeetingList({ workspaceId, meetings, empty, joinable = false }: ClientMeetingListProps) {
  if (meetings.length === 0) {
    return <p className="client-meeting-list-empty rounded-lg border border-dashed p-6 text-sm text-muted-foreground">{empty}</p>;
  }

  return (
    <DividedList className="client-meeting-list">
      {meetings.map((meeting) => (
        <li key={meeting.id} className="client-meeting-item flex flex-wrap items-start justify-between gap-x-4 gap-y-3 p-4">
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
          {joinable && meeting.meetUrl && meeting.status === "Scheduled" ? <JoinMeetingButton title={meeting.title} url={meeting.meetUrl} /> : null}
        </li>
      ))}
    </DividedList>
  );
}
