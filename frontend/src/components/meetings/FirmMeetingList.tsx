"use client";

import Link from "next/link";
import { FirmMeetingActions } from "@/components/meetings/FirmMeetingActions";
import type { MeetingProjectOption } from "@/components/meetings/MeetingForm";
import { MeetingTime } from "@/components/meetings/MeetingTime";
import { SyncBadge } from "@/components/meetings/SyncBadge";
import { useRefreshWhileSyncing } from "@/components/meetings/useRefreshWhileSyncing";
import { Badge } from "@/components/reui/badge";
import { durationLabel, durationOf } from "@/lib/meetings/time";
import type { Meeting } from "@/lib/meetings/types";

type MeetingListProps = {
  workspaceId: string;
  meetings: Meeting[];
  clients: { id: string; displayName: string }[];
  projects: MeetingProjectOption[];
  /** What the server thinks "now" is, so the first paint in the browser matches what it sent. */
  now: string;
  /** Said when the list is empty. */
  empty: string;
  /** The client's own page already says whose meetings these are. */
  showClient?: boolean;
};

/** A list of meetings with what a member can do with each (`FirmMeetingActions`). */
export function FirmMeetingList({ workspaceId, meetings, clients, projects, now, empty, showClient = true }: MeetingListProps) {
  useRefreshWhileSyncing(meetings, now);

  if (meetings.length === 0) {
    return <p className="meeting-list-empty rounded-lg border border-dashed p-6 text-sm text-muted-foreground">{empty}</p>;
  }

  const nowMs = Date.parse(now);

  return (
    <ul className="meeting-list flex flex-col divide-y rounded-lg border">
      {meetings.map((meeting) => {
        const cancelled = meeting.status === "Cancelled";
        // A problem still matters while the meeting has not happened: the client may be waiting for an invitation, or for news that it was called off.
        const unsent = Date.parse(meeting.endsAtUtc) > nowMs && meeting.syncStatus !== "Synced";

        return (
          <li key={meeting.id} className="meeting-item flex flex-wrap items-start justify-between gap-x-4 gap-y-3 p-4">
            <div className="meeting-info min-w-0">
              <div className="meeting-title-row flex flex-wrap items-center gap-2">
                <p className="font-medium">{meeting.title}</p>
                {cancelled ? <Badge variant="outline">Cancelled</Badge> : null}
                {unsent ? <SyncBadge meeting={meeting} /> : null}
              </div>
              <MeetingTime startsAtUtc={meeting.startsAtUtc} endsAtUtc={meeting.endsAtUtc} className="mt-1 block text-sm" />
              <p className="meeting-meta mt-1 text-sm text-muted-foreground">
                {showClient ? (
                  <Link href={`/w/${workspaceId}/clients/${meeting.client.id}`} className="hover:text-foreground hover:underline">
                    {meeting.client.displayName}
                  </Link>
                ) : null}
                {showClient ? " · " : null}
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
              {meeting.description ? <p className="meeting-notes mt-2 whitespace-pre-line text-sm text-muted-foreground">{meeting.description}</p> : null}
            </div>

            <FirmMeetingActions workspaceId={workspaceId} meeting={meeting} clients={clients} projects={projects} now={now} />
          </li>
        );
      })}
    </ul>
  );
}
