import { buildTimeline, isoDate } from "@/lib/projects/timeline";
import type { ProjectStatus, Stage } from "@/lib/projects/types";

/** What the calendar needs of a project: the freelancer's `Project` and the portal's `ClientProject` both fit. */
export type CalendarSource = {
  id: string;
  name: string;
  status: ProjectStatus;
  startDate: string | null;
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
  /** The last day it covers ("YYYY-MM-DD"), the same as `date` for a single day. */
  endDate: string;
  title: string;
  kind: "project" | "stage" | "meeting";
  /** Null for a meeting, which is not about one project. */
  projectId: string | null;
  projectName: string;
  clientName: string | null;
  late: boolean;
  /** A meeting that was called off: it stays on the calendar, marked. */
  cancelled: boolean;
  /** Only meetings have a time of day. */
  startsAtUtc: string | null;
  endsAtUtc: string | null;
};

/** A "YYYY-MM" month. */
export type Month = string;

/**
 * Open work with a date: the project's end and every unfinished stage (a completed project has nothing left to plan), and the
 * meetings, the ones that already took place and the ones called off included (marked `cancelled`). A stage covers the days it has on the project's
 * timeline (see `stageSpans`); the project's end is one day.
 */
export function buildEvents(sources: CalendarSource[], today: string, meetings: CalendarMeeting[] = []): CalendarEvent[] {
  const events: CalendarEvent[] = [];

  for (const source of sources) {
    if (source.status === "Completed") {
      continue;
    }
    const base = { projectId: source.id, projectName: source.name, clientName: source.clientName, startsAtUtc: null, endsAtUtc: null };

    if (source.dueDate) {
      events.push({ ...base, key: source.id, date: source.dueDate, endDate: source.dueDate, title: source.name, kind: "project", late: source.dueDate < today, cancelled: false });
    }
    const spans = stageSpans(source, today);
    for (const stage of source.stages) {
      if (stage.dueDate && stage.status !== "Done") {
        const span = spans.get(stage.id) ?? { from: stage.dueDate, to: stage.dueDate };
        events.push({ ...base, key: stage.id, date: span.from, endDate: span.to, title: stage.name, kind: "stage", late: stage.dueDate < today, cancelled: false });
      }
    }
  }

  for (const meeting of meetings) {
    events.push({
      key: meeting.id,
      date: meeting.startsAtUtc.slice(0, 10),
      endDate: meeting.endsAtUtc.slice(0, 10),
      title: meeting.title,
      kind: "meeting",
      projectId: null,
      projectName: "",
      clientName: meeting.clientName,
      late: false,
      cancelled: meeting.status === "Cancelled",
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

/**
 * The days each dated stage covers, as on the project's timeline: a stage runs from the day after the previous one ends (the first, from the
 * project's start) to its own end, the last one reaching the project's end. Without usable dates on the project, a stage is just its own day.
 */
function stageSpans(source: CalendarSource, today: string): Map<string, { from: string; to: string }> {
  const timeline = buildTimeline(source, today);
  const spans = new Map<string, { from: string; to: string }>();
  if (timeline.mode !== "time") {
    return spans;
  }

  timeline.segments.forEach(({ stage, fromDay, toDay }, index) => {
    if (stage.dueDate) {
      const to = Math.max(toDay, fromDay);
      const from = Math.min(index === 0 ? fromDay : fromDay + 1, to);
      spans.set(stage.id, { from: isoDate(from), to: isoDate(to) });
    }
  });
  return spans;
}

export const todayUtc = (): string => new Date().toISOString().slice(0, 10);

/** The month of a calendar date: "2026-11-16" -> "2026-11". */
export const monthOf = (date: string): Month => date.slice(0, 7);

/** The month from `?month=`, or `fallback` when it is missing or not a real month (the value comes straight from the URL). */
export function parseMonth(value: string | string[] | undefined, fallback: Month): Month {
  return typeof value === "string" && /^(20\d\d|2100)-(0[1-9]|1[0-2])$/.test(value) ? value : fallback;
}
