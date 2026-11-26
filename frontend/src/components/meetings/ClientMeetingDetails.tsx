"use client";

import Link from "next/link";
import { JoinMeetingButton } from "@/components/meetings/JoinMeetingButton";
import { MeetingTime } from "@/components/meetings/MeetingTime";
import { Badge } from "@/components/reui/badge";
import { DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { durationLabel, durationOf } from "@/lib/meetings/time";
import type { ClientMeeting } from "@/lib/meetings/types";

type ClientMeetingDetailsProps = {
  workspaceId: string;
  meeting: ClientMeeting;
  /** What the server thinks "now" is, so the first paint in the browser matches what it sent. */
  now: string;
};

/** What a calendar dialog shows for a meeting to a client: when, about what, the notes and a way into the call. Reading only; goes inside a `DialogContent`. */
export function ClientMeetingDetails({ workspaceId, meeting, now }: ClientMeetingDetailsProps) {
  const joinable = meeting.status === "Scheduled" && meeting.meetUrl !== null && Date.parse(meeting.endsAtUtc) > Date.parse(now);

  return (
    <>
      <DialogHeader>
        <DialogTitle>{meeting.title}</DialogTitle>
        <DialogDescription>
          <MeetingTime startsAtUtc={meeting.startsAtUtc} endsAtUtc={meeting.endsAtUtc} />
        </DialogDescription>
      </DialogHeader>

      <div className="meeting-details flex flex-col gap-3 text-sm">
        {meeting.status === "Cancelled" ? (
          <div className="meeting-details-badges">
            <Badge variant="outline">Cancelled</Badge>
          </div>
        ) : null}
        <p className="meeting-details-meta text-muted-foreground">
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
        {meeting.description ? <p className="meeting-details-notes whitespace-pre-line text-muted-foreground">{meeting.description}</p> : null}
      </div>

      {joinable ? (
        <div className="meeting-actions-block flex justify-end">
          <JoinMeetingButton title={meeting.title} url={meeting.meetUrl!} />
        </div>
      ) : null}
    </>
  );
}
