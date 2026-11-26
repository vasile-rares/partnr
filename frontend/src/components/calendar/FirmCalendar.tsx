"use client";

import { useState } from "react";
import { CalendarView } from "@/components/calendar/CalendarView";
import { FirmMeetingDetails } from "@/components/meetings/FirmMeetingDetails";
import type { MeetingProjectOption } from "@/components/meetings/MeetingForm";
import { useRefreshWhileSyncing } from "@/components/meetings/useRefreshWhileSyncing";
import { Dialog, DialogContent } from "@/components/ui/dialog";
import type { CalendarEvent, Month } from "@/lib/calendar/calendar";
import type { Meeting } from "@/lib/meetings/types";

type FirmCalendarProps = {
  workspaceId: string;
  events: CalendarEvent[];
  meetings: Meeting[];
  clients: { id: string; displayName: string }[];
  projects: MeetingProjectOption[];
  month: Month;
  today: string;
  /** What the server thinks "now" is, so the first paint in the browser matches what it sent. */
  now: string;
};

/** The calendar as a member uses it: a click on a meeting opens it, with the actions of `FirmMeetingActions`. */
export function FirmCalendar({ workspaceId, events, meetings, clients, projects, month, today, now }: FirmCalendarProps) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = meetings.find((meeting) => meeting.id === selectedId) ?? null;
  useRefreshWhileSyncing(meetings, now);

  return (
    <>
      <CalendarView events={events} month={month} today={today} projectsPath={`/w/${workspaceId}/projects`} onMeetingClick={setSelectedId} />
      <Dialog open={selected !== null} onOpenChange={(open) => (open ? null : setSelectedId(null))}>
        <DialogContent className="meeting-details-dialog max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg">
          {selected ? <FirmMeetingDetails workspaceId={workspaceId} meeting={selected} clients={clients} projects={projects} now={now} /> : null}
        </DialogContent>
      </Dialog>
    </>
  );
}
