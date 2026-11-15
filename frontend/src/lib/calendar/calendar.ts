import type { ProjectStatus, Stage } from "@/lib/projects/types";

/** What the calendar needs of a project: the freelancer's `Project` and the portal's `PortalProject` both fit. */
export type CalendarSource = {
  id: string;
  name: string;
  status: ProjectStatus;
  dueDate: string | null;
  stages: Stage[];
  /** Only the freelancer's side knows it. */
  clientName: string | null;
};

export type CalendarEvent = {
  key: string;
  /** "YYYY-MM-DD", a day with no time or zone. */
  date: string;
  title: string;
  kind: "project" | "stage";
  projectId: string;
  projectName: string;
  clientName: string | null;
  late: boolean;
};

/** A "YYYY-MM" month. */
export type Month = string;

/** Open work with a date: the project's end and every unfinished stage. A completed project has nothing left to plan. */
export function buildEvents(sources: CalendarSource[], today: string): CalendarEvent[] {
  const events: CalendarEvent[] = [];

  for (const source of sources) {
    if (source.status === "Completed") {
      continue;
    }
    const base = { projectId: source.id, projectName: source.name, clientName: source.clientName };

    if (source.dueDate) {
      events.push({ ...base, key: source.id, date: source.dueDate, title: source.name, kind: "project", late: source.dueDate < today });
    }
    for (const stage of source.stages) {
      if (stage.dueDate && stage.status !== "Done") {
        events.push({ ...base, key: stage.id, date: stage.dueDate, title: stage.name, kind: "stage", late: stage.dueDate < today });
      }
    }
  }

  return events.sort((a, b) => a.date.localeCompare(b.date) || a.title.localeCompare(b.title));
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

/** The events by day, for a grid to look up. */
export function eventsByDay(events: CalendarEvent[]): Map<string, CalendarEvent[]> {
  const byDay = new Map<string, CalendarEvent[]>();
  for (const event of events) {
    byDay.set(event.date, [...(byDay.get(event.date) ?? []), event]);
  }
  return byDay;
}
