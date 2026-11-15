import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import Link from "next/link";
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
};

export function CalendarView({ events, month, today, basePath, projectsPath }: CalendarViewProps) {
  const byDay = eventsByDay(events);
  const monthHref = (target: Month) => (target === monthOf(today) ? basePath : `${basePath}?month=${target}`);
  const inMonth = events.filter((event) => monthOf(event.date) === month);

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
          {WEEKDAYS.map((day) => (
            <div key={day} className="px-2 py-1.5">
              {day}
            </div>
          ))}
        </div>
        {monthWeeks(month).map((week) => (
          <div key={week[0]} className="calendar-week grid grid-cols-7 divide-x border-b last:border-b-0">
            {week.map((date) => {
              const dayEvents = byDay.get(date) ?? [];
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
                        <EventLink event={event} projectsPath={projectsPath} className="truncate px-1.5 py-0.5 text-xs" />
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
            {inMonth.map((event) => (
              <li key={event.key} className="calendar-agenda-item flex items-center justify-between gap-3 p-3">
                <EventLink event={event} projectsPath={projectsPath} className="min-w-0 truncate px-2 py-1 text-sm" />
                <time dateTime={event.date} className={cn("shrink-0 text-xs", event.late ? "text-destructive" : "text-muted-foreground")}>
                  {formatDate(event.date)}
                </time>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

function EventLink({ event, projectsPath, className }: { event: CalendarEvent; projectsPath: string; className: string }) {
  const context = event.kind === "stage" ? `Stage of ${event.projectName}` : "Project deadline";

  return (
    <Link
      href={`${projectsPath}/${event.projectId}`}
      title={[context, event.clientName].filter(Boolean).join(" · ")}
      className={cn(
        "calendar-event block rounded-md font-medium hover:underline",
        event.late
          ? "bg-destructive/15 text-destructive"
          : event.kind === "project"
            ? "bg-primary/15 text-primary"
            : "bg-muted text-foreground",
        className,
      )}
    >
      <span className="sr-only">{context}: </span>
      {event.title}
      {event.late ? <span className="sr-only"> (overdue)</span> : null}
    </Link>
  );
}
