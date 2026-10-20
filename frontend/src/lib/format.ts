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
