"use client";

import { TZDate } from "@date-fns/tz";
import { enGB } from "date-fns/locale/en-GB";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { useInBrowser } from "@/components/meetings/useInBrowser";
import { EventCalendar } from "@/components/reui/event-calendar/event-calendar";
import { EventCalendarContent } from "@/components/reui/event-calendar/event-calendar-content";
import { EventCalendarNav } from "@/components/reui/event-calendar/event-calendar-nav";
import type { EventCalendarI18nOverrides } from "@/components/reui/event-calendar/event-calendar-i18n";
import type { CalendarEvent as UiEvent, CalendarView as UiView } from "@/components/reui/event-calendar/event-calendar-types";
import { Card } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { monthOf, type CalendarEvent, type Month } from "@/lib/calendar/calendar";

const VIEWS: UiView[] = ["month", "week", "day", "agenda"];
const HEIGHT = "h-[calc(100svh-15rem)] min-h-[40rem]";

// The rest of the app writes 24-hour times and the day before the month.
const I18N: EventCalendarI18nOverrides = {
  formats: {
    eventTime: "HH:mm",
    timeGutter: "HH:mm",
    timeGutterMinute: "HH:mm",
    dayTitle: "EEEE, d MMMM yyyy",
    agendaDayHeader: "EEEE, d MMMM",
    moreDayHeader: "EEEE, d MMMM",
  },
};

type CalendarViewProps = {
  /** The meetings, and the project deadlines the reader can switch on. */
  events: CalendarEvent[];
  /** The month the calendar opens on. */
  month: Month;
  today: string;
  /** Where a project's page is: the event's link goes to `${projectsPath}/{projectId}`. */
  projectsPath: string;
  /** A click on a meeting; the page shows it. */
  onMeetingClick: (meetingId: string) => void;
};

// A client component because the calendar draws in the reader's time zone, which the server cannot know: until the browser has taken
// over there is only a placeholder, so nothing the server sent can differ from the first browser render (see `useInBrowser`).
export function CalendarView(props: CalendarViewProps) {
  const inBrowser = useInBrowser();
  const [showDeadlines, setShowDeadlines] = useState(false);

  return (
    <div className="calendar-section flex flex-col gap-3">
      <div className="calendar-options flex items-center justify-end gap-2">
        <Checkbox id="calendar-deadlines" checked={showDeadlines} onCheckedChange={(checked) => setShowDeadlines(checked === true)} />
        <Label htmlFor="calendar-deadlines" className="font-normal text-muted-foreground">
          Show deadlines
        </Label>
      </div>
      <Card className="calendar gap-0 overflow-hidden py-0">
        {inBrowser ? <Calendar {...props} showDeadlines={showDeadlines} /> : <Skeleton className={`calendar-placeholder ${HEIGHT} rounded-none`} />}
      </Card>
    </div>
  );
}

function Calendar({ events, month, today, projectsPath, onMeetingClick, showDeadlines }: CalendarViewProps & { showDeadlines: boolean }) {
  const router = useRouter();
  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  const uiEvents = useMemo(
    () =>
      events
        .filter((event) => showDeadlines || event.kind === "meeting")
        .map((event) => toUiEvent(event, timeZone, event.kind === "meeting" ? null : `${projectsPath}/${event.projectId}`)),
    [events, showDeadlines, timeZone, projectsPath],
  );
  const [defaultDate] = useState(() => (month === monthOf(today) ? new Date() : new TZDate(Number(month.slice(0, 4)), Number(month.slice(5)) - 1, 1, timeZone)));
  const [defaultView] = useState<UiView>(() => (window.matchMedia("(max-width: 767px)").matches ? "agenda" : "month"));

  return (
    <EventCalendar
      events={uiEvents}
      defaultDate={defaultDate}
      defaultView={defaultView}
      views={VIEWS}
      timeZone={timeZone}
      locale={enGB}
      i18n={I18N}
      weekStartsOn={1}
      interactions={{ drag: false, resize: false, selectSlot: false }}
      onEventClick={(occurrence) => {
        const { href, meetingId } = occurrence.event.data ?? {};
        if (meetingId) {
          onMeetingClick(meetingId);
        } else if (href) {
          router.push(href);
        }
      }}
      className={`calendar-grid ${HEIGHT}`}
    >
      <EventCalendarNav />
      <EventCalendarContent />
    </EventCalendar>
  );
}

/** A meeting is opened by the page (`meetingId`), a deadline goes to its project (`href`). */
type EventData = { href: string | null; meetingId: string | null };

// Deadlines are whole days in the reader's zone, meetings are the instants they are.
function toUiEvent(event: CalendarEvent, timeZone: string, href: string | null): UiEvent<EventData> {
  const base = { id: event.key, readOnly: true };

  if (event.startsAtUtc && event.endsAtUtc) {
    return {
      ...base,
      data: { href: null, meetingId: event.key },
      title: event.cancelled ? `${event.title} (cancelled)` : event.title,
      start: new Date(event.startsAtUtc),
      end: new Date(event.endsAtUtc),
      color: event.cancelled ? "var(--color-muted-foreground)" : "var(--color-violet-500)",
    };
  }

  const [year, monthNumber, day] = event.date.split("-").map(Number);
  const [endYear, endMonth, endDay] = event.endDate.split("-").map(Number);
  const title = event.kind === "stage" ? `${event.projectName}: ${event.title}` : event.title;
  return {
    ...base,
    data: { href, meetingId: null },
    title: event.late ? `${title} (overdue)` : title,
    start: new TZDate(year, monthNumber - 1, day, timeZone),
    end: new TZDate(endYear, endMonth - 1, endDay + 1, timeZone),
    allDay: true,
    color: event.late ? "var(--color-destructive)" : event.kind === "stage" ? "var(--color-teal-500)" : "var(--color-blue-500)",
  };
}
