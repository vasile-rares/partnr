import type { ProjectStatus, Stage } from "@/lib/projects/types";

/** What the calendar needs of a project: the freelancer's `Project` and the portal's `ClientProject` both fit. */
export type CalendarSource = {
  id: string;
  name: string;
  status: ProjectStatus;
  dueDate: string | null;
  stages: Stage[];
  /** Only the freelancer's side knows it. */
  clientName: string | null;
};

/** A call with a client. The freelancer's and the client's meetings both fit. */
export type CalendarMeeting = {
  id: string;
  title: string;
  startsAtUtc: string;
  endsAtUtc: string;
  status: "Scheduled" | "Cancelled";
  clientName: string | null;
};

export type CalendarEvent = {
  key: string;
  /** "YYYY-MM-DD", a day with no time or zone. For a meeting it is the UTC day, which the browser replaces by the reader's own. */
  date: string;
  title: string;
  kind: "project" | "stage" | "meeting";
  /** Null for a meeting, which is not about one project. */
  projectId: string | null;
  projectName: string;
  clientName: string | null;
  late: boolean;
  /** Only meetings have a time of day. */
  startsAtUtc: string | null;
  endsAtUtc: string | null;
};

/** A "YYYY-MM" month. */
export type Month = string;

/**
 * Open work with a date: the project's end and every unfinished stage (a completed project has nothing left to plan), and the
 * meetings that were not called off, the ones that already took place included.
 */
export function buildEvents(sources: CalendarSource[], today: string, meetings: CalendarMeeting[] = []): CalendarEvent[] {
  const events: CalendarEvent[] = [];

  for (const source of sources) {
    if (source.status === "Completed") {
      continue;
    }
    const base = { projectId: source.id, projectName: source.name, clientName: source.clientName, startsAtUtc: null, endsAtUtc: null };

    if (source.dueDate) {
      events.push({ ...base, key: source.id, date: source.dueDate, title: source.name, kind: "project", late: source.dueDate < today });
    }
    for (const stage of source.stages) {
      if (stage.dueDate && stage.status !== "Done") {
        events.push({ ...base, key: stage.id, date: stage.dueDate, title: stage.name, kind: "stage", late: stage.dueDate < today });
      }
    }
  }

  for (const meeting of meetings) {
    if (meeting.status === "Cancelled") {
      continue;
    }
    events.push({
      key: meeting.id,
      date: meeting.startsAtUtc.slice(0, 10),
      title: meeting.title,
      kind: "meeting",
      projectId: null,
      projectName: "",
      clientName: meeting.clientName,
      late: false,
      startsAtUtc: meeting.startsAtUtc,
      endsAtUtc: meeting.endsAtUtc,
    });
  }

  // On one day the meetings come first, by the hour; the deadlines follow.
  return events.sort(
    (a, b) =>
      a.date.localeCompare(b.date) ||
      (a.kind === "meeting" ? 0 : 1) - (b.kind === "meeting" ? 0 : 1) ||
      (a.startsAtUtc ?? "").localeCompare(b.startsAtUtc ?? "") ||
      a.title.localeCompare(b.title),
  );
}

export const todayUtc = (): string => new Date().toISOString().slice(0, 10);

/** The month of a calendar date: "2026-11-16" -> "2026-11". */
export const monthOf = (date: string): Month => date.slice(0, 7);

/** The month from `?month=`, or `fallback` when it is missing or not a real month (the value comes straight from the URL). */
export function parseMonth(value: string | string[] | undefined, fallback: Month): Month {
  return typeof value === "string" && /^(20\d\d|2100)-(0[1-9]|1[0-2])$/.test(value) ? value : fallback;
}

export function shiftMonth(month: Month, delta: number): Month {
  const [year, number] = month.split("-").map(Number);
  const shifted = new Date(Date.UTC(year, number - 1 + delta, 1));
  return shifted.toISOString().slice(0, 7);
}

/** Whole weeks, Monday first, that cover the month; the days around it belong to the neighbouring months. */
export function monthWeeks(month: Month): string[][] {
  const [year, number] = month.split("-").map(Number);
  const first = Date.UTC(year, number - 1, 1);
  const last = Date.UTC(year, number, 0);
  const DAY = 86_400_000;
  const mondayOffset = (new Date(first).getUTCDay() + 6) % 7;

  const weeks: string[][] = [];
  for (let start = first - mondayOffset * DAY; start <= last; start += 7 * DAY) {
    weeks.push(Array.from({ length: 7 }, (_, day) => new Date(start + day * DAY).toISOString().slice(0, 10)));
  }
  return weeks;
}

const monthName = new Intl.DateTimeFormat("en-GB", { month: "long", year: "numeric", timeZone: "UTC" });

/** "November 2026" */
export const monthLabel = (month: Month): string => monthName.format(new Date(`${month}-01T00:00:00Z`));

/** The events by day, for a grid to look up. `dayOf` says which day an event belongs to (a meeting's depends on the reader's time zone). */
export function eventsByDay(events: CalendarEvent[], dayOf: (event: CalendarEvent) => string = (event) => event.date): Map<string, CalendarEvent[]> {
  const byDay = new Map<string, CalendarEvent[]>();
  for (const event of events) {
    const day = dayOf(event);
    byDay.set(day, [...(byDay.get(day) ?? []), event]);
  }
  return byDay;
}
