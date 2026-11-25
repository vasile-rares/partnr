import { expect, test } from "@playwright/test";
import { formatDateInYear } from "@/lib/format";

// No browser here: the date format is a plain function of the date and the clock.
const now = Date.parse("2026-11-25T10:00:00Z");

test("a date in the current year drops the year", () => {
  expect(formatDateInYear("2026-12-15", now)).toBe("15 Dec");
  expect(formatDateInYear("2026-01-03", now)).toBe("3 Jan");
});

test("a date in another year keeps it", () => {
  expect(formatDateInYear("2027-01-05", now)).toBe("5 Jan 2027");
  expect(formatDateInYear("2025-12-31", now)).toBe("31 Dec 2025");
});

test("the year follows the clock in UTC", () => {
  expect(formatDateInYear("2027-06-01", Date.parse("2026-12-31T23:30:00Z"))).toBe("1 Jun 2027");
  expect(formatDateInYear("2027-06-01", Date.parse("2027-01-01T00:30:00Z"))).toBe("1 Jun");
});
