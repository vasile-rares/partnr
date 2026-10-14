// UTC on purpose: the same instant reads the same on the server and in the browser, so there is nothing to mismatch on hydration.
const dateTime = new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" });

/** "14 Oct 2026, 09:00 UTC" */
export const formatDateTimeUtc = (iso: string): string => `${dateTime.format(new Date(iso))} UTC`;

const calendarDate = new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeZone: "UTC" });

/** "15 Dec 2026", for a calendar date ("YYYY-MM-DD") with no time or zone of its own. */
export const formatDate = (date: string): string => calendarDate.format(new Date(`${date}T00:00:00Z`));
