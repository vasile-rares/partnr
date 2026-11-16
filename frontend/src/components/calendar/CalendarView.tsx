"use client";

import { ChevronLeftIcon, ChevronRightIcon, VideoIcon } from "lucide-react";
import Link from "next/link";
import { useMemo } from "react";
import { useInBrowser, useTimeZone } from "@/components/meetings/useInBrowser";
import { buttonVariants } from "@/components/ui/button";
import {
  eventsByDay,
  monthLabel,
  monthOf,
  monthWeeks,
  shiftMonth,
  type CalendarEvent,
  type Month,
} from "@/lib/calendar/calendar";
import { formatDate } from "@/lib/format";
import { clockOf, dayOf, localDateOf } from "@/lib/meetings/time";
import { cn } from "@/lib/utils";

const WEEKDAYS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

type CalendarViewProps = {
  events: CalendarEvent[];
  month: Month;
  today: string;
  /** The calendar page's own address: the month is added to it as `?month=`. */
  basePath: string;
  /** Where a project's page is: the event's link goes to `${projectsPath}/{projectId}`. */
  projectsPath: string;
  /** Where the meetings are listed: a meeting's link goes there. */
  meetingsPath: string;
};

// A client component only because a meeting belongs to the day it is on for the reader, which the server cannot know: the server
// puts it on its UTC day and the browser moves it, once it has taken over (see `useInBrowser`). A deadline is a day with no zone.
export function CalendarView({ events, month, today, basePath, projectsPath, meetingsPath }: CalendarViewProps) {
  const inBrowser = useInBrowser();
  const day = (event: CalendarEvent) => (inBrowser && event.startsAtUtc ? localDateOf(event.startsAtUtc) : event.date);
  const byDay = useMemo(() => eventsByDay(events, day), [events, inBrowser]); // eslint-disable-line react-hooks/exhaustive-deps
  const monthHref = (target: Month) => (target === monthOf(today) ? basePath : `${basePath}?month=${target}`);
  const inMonth = events.filter((event) => monthOf(day(event)) === month);
  const hrefOf = (event: CalendarEvent) => (event.kind === "meeting" ? meetingsPath : `${projectsPath}/${event.projectId}`);

  return (
    <section aria-label="Calendar" className="calendar flex flex-col gap-4">
      <div className="calendar-header flex flex-wrap items-center justify-between gap-3">
        <h2 aria-live="polite" className="font-heading text-xl font-semibold">
          {monthLabel(month)}
        </h2>
        <nav aria-label="Month" className="calendar-nav flex items-center gap-2">
          <Link
            href={monthHref(shiftMonth(month, -1))}
            aria-label="Previous month"
            className={buttonVariants({ variant: "outline", size: "icon" })}
          >
            <ChevronLeftIcon aria-hidden />
          </Link>
          <Link href={basePath} className={buttonVariants({ variant: "outline" })}>
            Today
          </Link>
          <Link
            href={monthHref(shiftMonth(month, 1))}
            aria-label="Next month"
            className={buttonVariants({ variant: "outline", size: "icon" })}
          >
            <ChevronRightIcon aria-hidden />
          </Link>
        </nav>
      </div>

      <div className="calendar-grid hidden overflow-hidden rounded-lg border md:block">
        <div className="calendar-weekdays grid grid-cols-7 border-b bg-muted/40 text-xs font-medium text-muted-foreground">
          {WEEKDAYS.map((weekday) => (
            <div key={weekday} className="px-2 py-1.5">
              {weekday}
            </div>
          ))}
        </div>
        {monthWeeks(month).map((week) => (
          <div key={week[0]} className="calendar-week grid grid-cols-7 divide-x border-b last:border-b-0">
            {week.map((date) => {
              const dayEvents = inTimeOrder(byDay.get(date) ?? []);
              const outside = monthOf(date) !== month;
              return (
                <div
                  key={date}
                  data-date={date}
                  className={cn("calendar-day flex min-h-24 min-w-0 flex-col gap-1 p-1.5", outside && "bg-muted/30")}
                >
                  <span
                    className={cn(
                      "calendar-day-number flex size-6 items-center justify-center self-start rounded-full text-xs",
                      outside ? "text-muted-foreground/60" : "text-muted-foreground",
                      date === today && "bg-primary font-semibold text-primary-foreground",
                    )}
                  >
                    {Number(date.slice(8))}
                  </span>
                  <ul className="calendar-day-events flex flex-col gap-1">
                    {dayEvents.map((event) => (
                      <li key={event.key}>
                        <EventLink event={event} href={hrefOf(event)} className="truncate px-1.5 py-0.5 text-xs" />
                      </li>
                    ))}
                  </ul>
                </div>
              );
            })}
          </div>
        ))}
      </div>

      <div className="calendar-agenda md:hidden">
        {inMonth.length === 0 ? (
          <p className="text-sm text-muted-foreground">Nothing is due in {monthLabel(month)}.</p>
        ) : (
          <ul className="calendar-agenda-list flex flex-col divide-y rounded-lg border">
            {[...inMonth]
              .sort((a, b) => day(a).localeCompare(day(b)))
              .map((event) => (
                <li key={event.key} className="calendar-agenda-item flex items-center justify-between gap-3 p-3">
                  <EventLink event={event} href={hrefOf(event)} className="min-w-0 truncate px-2 py-1 text-sm" />
                  <EventWhen event={event} day={day(event)} />
                </li>
              ))}
          </ul>
        )}
      </div>
    </section>
  );
}

// Meetings by the hour, then the deadlines.
const inTimeOrder = (events: CalendarEvent[]): CalendarEvent[] =>
  [...events].sort((a, b) => (a.startsAtUtc ? 0 : 1) - (b.startsAtUtc ? 0 : 1) || (a.startsAtUtc ?? "").localeCompare(b.startsAtUtc ?? ""));

function EventWhen({ event, day }: { event: CalendarEvent; day: string }) {
  const timeZone = useTimeZone();

  return (
    <time dateTime={event.startsAtUtc ?? event.date} className={cn("shrink-0 text-xs", event.late ? "text-destructive" : "text-muted-foreground")}>
      {event.startsAtUtc ? `${dayOf(event.startsAtUtc, timeZone)} · ${clockOf(event.startsAtUtc, timeZone)}` : formatDate(day)}
    </time>
  );
}

function EventLink({ event, href, className }: { event: CalendarEvent; href: string; className: string }) {
  const timeZone = useTimeZone();
  const meeting = event.kind === "meeting";
  const context = meeting ? "Meeting" : event.kind === "stage" ? `Stage of ${event.projectName}` : "Project deadline";

  return (
    <Link
      href={href}
      title={[context, event.clientName].filter(Boolean).join(" · ")}
      className={cn(
        "calendar-event block rounded-md font-medium hover:underline",
        event.late
          ? "bg-destructive/15 text-destructive"
          : meeting
            ? "border-l-2 border-primary bg-secondary text-secondary-foreground"
            : event.kind === "project"
              ? "bg-primary/15 text-primary"
              : "bg-muted text-foreground",
        className,
      )}
    >
      <span className="sr-only">{context}: </span>
      {meeting && event.startsAtUtc ? (
        <>
          <VideoIcon aria-hidden className="mr-1 inline size-3" />
          <span className="tabular-nums">{clockOf(event.startsAtUtc, timeZone)}</span>{" "}
        </>
      ) : null}
      {event.title}
      {event.late ? <span className="sr-only"> (overdue)</span> : null}
    </Link>
  );
}
