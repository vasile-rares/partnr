import Link from "next/link";
import { Section } from "@/components/layout/Section";
import { JoinMeetingButton } from "@/components/meetings/JoinMeetingButton";
import { MeetingTime } from "@/components/meetings/MeetingTime";
import type { ClientMeeting } from "@/lib/meetings/types";

/** The next call with the workspace, on the client's home page; nothing when none is planned. */
export function ClientNextMeeting({ workspaceId, meeting }: { workspaceId: string; meeting: ClientMeeting | null }) {
  if (!meeting) {
    return null;
  }

  return (
    <Section title="Next meeting" className="client-next-meeting">
      <div className="client-next-meeting-card flex flex-wrap items-center justify-between gap-4 rounded-lg border p-4">
        <div className="min-w-0">
          <p className="font-medium">{meeting.title}</p>
          <MeetingTime startsAtUtc={meeting.startsAtUtc} endsAtUtc={meeting.endsAtUtc} className="mt-1 block text-sm" />
          {meeting.project ? <p className="mt-1 text-sm text-muted-foreground">{meeting.project.name}</p> : null}
        </div>
        <div className="flex items-center gap-3">
          <Link href={`/w/${workspaceId}/meetings`} className="text-sm text-muted-foreground underline underline-offset-4 hover:text-foreground">
            All meetings
          </Link>
          {meeting.meetUrl ? <JoinMeetingButton title={meeting.title} url={meeting.meetUrl} /> : null}
        </div>
      </div>
    </Section>
  );
}
