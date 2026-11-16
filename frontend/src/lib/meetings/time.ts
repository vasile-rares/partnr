import { fromLocalDate } from "@/lib/format";
import type { Timed } from "@/lib/meetings/types";

export const DURATIONS = [15, 30, 45, 60, 90, 120];

export const DEFAULT_DURATION = 30;

/** The clock, kept out of components (the lint rules want render to be pure). Only for server pages and event handlers. */
export const nowIso = (): string => new Date().toISOString();

/** True when the moment has passed. */
export const isPast = (iso: string): boolean => Date.parse(iso) <= Date.now();

/** Scheduled and not over yet: Upcoming. Everything else (over, or called off) is Past, the latest first. */
export function splitMeetings<T extends Timed>(meetings: T[], now: string): { upcoming: T[]; past: T[] } {
  const upcoming: T[] = [];
  const past: T[] = [];

  for (const meeting of meetings) {
    (meeting.status === "Scheduled" && Date.parse(meeting.endsAtUtc) > Date.parse(now) ? upcoming : past).push(meeting);
  }

  return {
    upcoming: upcoming.sort((a, b) => a.startsAtUtc.localeCompare(b.startsAtUtc)),
    past: past.sort((a, b) => b.startsAtUtc.localeCompare(a.startsAtUtc)),
  };
}

/** The moments for a date ("YYYY-MM-DD") and a time ("HH:MM") the person chose, which are in their own time zone. Null when either is missing or wrong. */
export function momentsOf(date: string, time: string, minutes: number): { startsAt: string; endsAt: string } | null {
  const [year, month, day] = date.split("-").map(Number);
  const [hours, mins] = time.split(":").map(Number);
  if ([year, month, day, hours, mins].some((part) => !Number.isInteger(part)) || !(minutes > 0)) {
    return null;
  }

  const start = new Date(year, month - 1, day, hours, mins);
  return { startsAt: start.toISOString(), endsAt: new Date(start.getTime() + minutes * 60_000).toISOString() };
}

export const durationOf = (meeting: Pick<Timed, "startsAtUtc" | "endsAtUtc">): number =>
  Math.round((Date.parse(meeting.endsAtUtc) - Date.parse(meeting.startsAtUtc)) / 60_000);

/** "YYYY-MM-DD" in the reader's own time zone. */
export const localDateOf = (iso: string): string => fromLocalDate(new Date(iso));

/** "HH:MM" in the reader's own time zone, for a time input. */
export function localTimeOf(iso: string): string {
  const date = new Date(iso);
  return `${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
}

/** "1 h", "45 min", "1 h 30 min". */
export function durationLabel(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return [hours > 0 ? `${hours} h` : null, rest > 0 || hours === 0 ? `${rest} min` : null].filter(Boolean).join(" ");
}

// `timeZone` is "UTC" on the server and for the first paint in the browser, then undefined, which is the reader's own: see `useInBrowser`.
const dayFormat = (timeZone?: string) =>
  new Intl.DateTimeFormat("en-GB", { weekday: "short", day: "numeric", month: "short", timeZone });
const clockFormat = (timeZone?: string) =>
  new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", hour12: false, timeZone });
const zoneFormat = (timeZone?: string) => new Intl.DateTimeFormat("en-GB", { timeZoneName: "short", timeZone });

/** "14:30" */
export const clockOf = (iso: string, timeZone?: string): string => clockFormat(timeZone).format(new Date(iso));

/** "Fri 20 Nov" */
export const dayOf = (iso: string, timeZone?: string): string => dayFormat(timeZone).format(new Date(iso));

/** "UTC", "EET", "GMT+2": what the zone is called next to a time. */
export const zoneName = (iso: string, timeZone?: string): string =>
  zoneFormat(timeZone).formatToParts(new Date(iso)).find((part) => part.type === "timeZoneName")?.value ?? "";

/** "Fri 20 Nov · 09:30–10:00 EET" (an end on another day gets its own day). */
export function whenLabel(meeting: Pick<Timed, "startsAtUtc" | "endsAtUtc">, timeZone?: string): string {
  const sameDay = dayOf(meeting.startsAtUtc, timeZone) === dayOf(meeting.endsAtUtc, timeZone);
  const end = sameDay ? clockOf(meeting.endsAtUtc, timeZone) : `${dayOf(meeting.endsAtUtc, timeZone)} ${clockOf(meeting.endsAtUtc, timeZone)}`;

  return `${dayOf(meeting.startsAtUtc, timeZone)} · ${clockOf(meeting.startsAtUtc, timeZone)}–${end} ${zoneName(meeting.startsAtUtc, timeZone)}`;
}
