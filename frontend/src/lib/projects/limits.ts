import { formatDate } from "@/lib/format";

/** The project's own dates: a stage cannot be due outside them (the API refuses it too). Either can be missing. */
export type DateLimits = { min: string | null; max: string | null };

/** "1 Oct 2026 – 15 Dec 2026", "from 1 Oct 2026", "until 15 Dec 2026", or nothing when the project has no dates. */
export function limitsText({ min, max }: DateLimits): string | null {
  if (min && max) {
    return `${formatDate(min)} – ${formatDate(max)}`;
  }
  return min ? `from ${formatDate(min)}` : max ? `until ${formatDate(max)}` : null;
}

/** Calendar dates ("YYYY-MM-DD") compare as text; an empty date is no date, so it is always fine. */
export const outsideLimits = (date: string, { min, max }: DateLimits): boolean =>
  date !== "" && ((min !== null && date < min) || (max !== null && date > max));
