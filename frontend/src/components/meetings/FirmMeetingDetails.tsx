"use client";

import Link from "next/link";
import { FirmMeetingActions } from "@/components/meetings/FirmMeetingActions";
import type { MeetingProjectOption } from "@/components/meetings/MeetingForm";
import { MeetingTime } from "@/components/meetings/MeetingTime";
import { SyncBadge } from "@/components/meetings/SyncBadge";
import { Badge } from "@/components/reui/badge";
import { DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { durationLabel, durationOf } from "@/lib/meetings/time";
import type { Meeting } from "@/lib/meetings/types";

type FirmMeetingDetailsProps = {
  workspaceId: string;
  meeting: Meeting;
  clients: { id: string; displayName: string }[];
  projects: MeetingProjectOption[];
  /** What the server thinks "now" is, so the first paint in the browser matches what it sent. */
  now: string;
};

/** What a calendar dialog shows for a meeting to a member: when, with whom, the notes and the actions. Goes inside a `DialogContent`. */
export function FirmMeetingDetails({ workspaceId, meeting, clients, projects, now }: FirmMeetingDetailsProps) {
  const cancelled = meeting.status === "Cancelled";
  const unsent = Date.parse(meeting.endsAtUtc) > Date.parse(now) && meeting.syncStatus !== "Synced";

  return (
    <>
      <DialogHeader>
        <DialogTitle>{meeting.title}</DialogTitle>
        <DialogDescription>
          <MeetingTime startsAtUtc={meeting.startsAtUtc} endsAtUtc={meeting.endsAtUtc} />
        </DialogDescription>
      </DialogHeader>

      <div className="meeting-details flex flex-col gap-3 text-sm">
        {cancelled || unsent ? (
          <div className="meeting-details-badges flex flex-wrap items-center gap-2">
            {cancelled ? <Badge variant="outline">Cancelled</Badge> : null}
            {unsent ? <SyncBadge meeting={meeting} /> : null}
          </div>
        ) : null}
        <p className="meeting-details-meta text-muted-foreground">
          <Link href={`/w/${workspaceId}/clients/${meeting.client.id}`} className="hover:text-foreground hover:underline">
            {meeting.client.displayName}
          </Link>
          {" · "}
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

      <FirmMeetingActions workspaceId={workspaceId} meeting={meeting} clients={clients} projects={projects} now={now} />
    </>
  );
}
