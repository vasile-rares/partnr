"use client";

import { useState } from "react";
import { CalendarView } from "@/components/calendar/CalendarView";
import { ClientMeetingDetails } from "@/components/meetings/ClientMeetingDetails";
import { Dialog, DialogContent } from "@/components/ui/dialog";
import type { CalendarEvent, Month } from "@/lib/calendar/calendar";
import type { ClientMeeting } from "@/lib/meetings/types";

type ClientCalendarProps = {
  workspaceId: string;
  events: CalendarEvent[];
  meetings: ClientMeeting[];
  month: Month;
  today: string;
  /** What the server thinks "now" is, so the first paint in the browser matches what it sent. */
  now: string;
};

/** The calendar as a client uses it: a click on a meeting opens it, to read and to join. */
export function ClientCalendar({ workspaceId, events, meetings, month, today, now }: ClientCalendarProps) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = meetings.find((meeting) => meeting.id === selectedId) ?? null;

  return (
    <>
      <CalendarView events={events} month={month} today={today} projectsPath={`/w/${workspaceId}/projects`} onMeetingClick={setSelectedId} />
      <Dialog open={selected !== null} onOpenChange={(open) => (open ? null : setSelectedId(null))}>
        <DialogContent className="meeting-details-dialog max-h-[calc(100dvh-2rem)] overflow-y-auto sm:max-w-lg">
          {selected ? <ClientMeetingDetails workspaceId={workspaceId} meeting={selected} now={now} /> : null}
        </DialogContent>
      </Dialog>
    </>
  );
}
