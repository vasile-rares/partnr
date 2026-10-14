// UTC on purpose: the same instant reads the same on the server and in the browser, so there is nothing to mismatch on hydration.
const dateTime = new Intl.DateTimeFormat("en-GB", { dateStyle: "medium", timeStyle: "short", timeZone: "UTC" });

/** "14 Oct 2026, 09:00 UTC" */
export const formatDateTimeUtc = (iso: string): string => `${dateTime.format(new Date(iso))} UTC`;
