// UTC on purpose: the same instant reads the same on the server and in the browser, so there is nothing to mismatch on hydration.
const dateTime = new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" });

/** "14 Oct 2026, 09:00 UTC" */
export const formatDateTimeUtc = (iso: string): string => `${dateTime.format(new Date(iso))} UTC`;

/** "820 B", "340 KB", "4.2 MB" */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${Math.round(bytes / 1024)} KB`;
  }
  return `${Number((bytes / (1024 * 1024)).toFixed(1))} MB`;
}

/** "Ana Pop" -> "AP", "Studio" -> "S" (at most two letters). */
export function initials(name: string): string {
  const letters = name
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .map((word) => Array.from(word)[0]);
  return (letters.length > 1 ? letters[0] + letters[letters.length - 1] : (letters[0] ?? "?")).toUpperCase();
}

const calendarDate =new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeZone: "UTC" });

/** "15 Dec 2026", for a calendar date ("YYYY-MM-DD") with no time or zone of its own. */
export const formatDate = (date: string): string => calendarDate.format(new Date(`${date}T00:00:00Z`));

const dayAndMonth = new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", timeZone: "UTC" });

/** "15 Dec", for a label with little room. */
export const formatDayMonth = (date: string): string => dayAndMonth.format(new Date(`${date}T00:00:00Z`));

const DAY_MS = 24 * 60 * 60 * 1000;

/** "3 days overdue", "Today", "Tomorrow", "In 5 days", for a number of days from `daysUntil`. */
export function dueIn(days: number): string {
  if (days < 0) {
    return days === -1 ? "1 day overdue" : `${-days} days overdue`;
  }
  if (days === 0) {
    return "Today";
  }
  return days === 1 ? "Tomorrow" : `In ${days} days`;
}

/** Whole days from today (UTC) to a calendar date ("YYYY-MM-DD"): 0 is today, negative is in the past. */
export function daysUntil(date: string): number {
  const today = Math.floor(Date.now() / DAY_MS) * DAY_MS;
  return Math.round((Date.parse(`${date}T00:00:00Z`) - today) / DAY_MS);
}

/** A calendar date ("YYYY-MM-DD") as a local Date at midnight, for a calendar to draw; no time zone moves it a day. */
export function toLocalDate(date: string): Date {
  const [year, month, day] = date.split("-").map(Number);
  return new Date(year, month - 1, day);
}

/** The calendar date ("YYYY-MM-DD") of a local Date, the other way round from `toLocalDate`. */
export const fromLocalDate = (date: Date): string =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
