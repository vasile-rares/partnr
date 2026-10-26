import type { Stage } from "@/lib/projects/types";

const DAY_MS = 24 * 60 * 60 * 1000;

/** A calendar date ("YYYY-MM-DD") as a whole day count, so dates can be added to and compared as numbers. */
export const dayNumber = (date: string): number => Math.round(Date.parse(`${date}T00:00:00Z`) / DAY_MS);
export const isoDate = (day: number): string => new Date(day * DAY_MS).toISOString().slice(0, 10);

export const todayUtc = (): string => new Date().toISOString().slice(0, 10);

/** The order the bar draws the stages in: by due date, a stage without a date keeping the place it has in the list. */
export function orderByDate(stages: Stage[]): Stage[] {
  const dated = stages.filter((stage) => stage.dueDate !== null).sort((a, b) => a.dueDate!.localeCompare(b.dueDate!));
  let next = 0;
  return stages.map((stage) => (stage.dueDate !== null ? dated[next++] : stage));
}

export type TimelineInput = {
  startDate: string | null;
  dueDate: string | null;
  stages: Stage[];
};

export type TimelineSegment = {
  stage: Stage;
  /** Where the segment starts and how wide it is, as fractions of the bar (0 to 1). */
  left: number;
  width: number;
  /** The days (see `dayNumber`) the segment spans in "time" mode, so a drag on the bar can be turned back into a date. */
  fromDay: number;
  toDay: number;
};

export type Timeline = {
  /** "time": widths follow the dates. "even": no usable dates, so every stage gets the same width. */
  mode: "time" | "even";
  segments: TimelineSegment[];
  /** The bar covers `start` to `end` in "time" mode. */
  start: string | null;
  end: string | null;
  /** Where today falls on the bar (0 to 1); null in "even" mode. Pinned to an end when today is outside the dates. */
  today: number | null;
  state: "before" | "running" | "over" | null;
};

/**
 * Lays the stages along the project's dates, in date order (see `orderByDate`). A stage runs from the previous stage's date to its own; a stage without a
 * date takes an equal share of the gap between its dated neighbours, and the last one always reaches the end. Dates that
 * go backwards are held at the previous one, and no stage is narrower than a minimum, so a short stage stays clickable.
 */
export function buildTimeline({ startDate, dueDate, stages: unordered }: TimelineInput, today: string): Timeline {
  const stages = orderByDate(unordered);
  const count = stages.length;
  if (count === 0) {
    return { mode: "even", segments: [], start: null, end: null, today: null, state: null };
  }

  const stageDays = stages.map((stage) => (stage.dueDate ? dayNumber(stage.dueDate) : null));
  const datedDays = stageDays.filter((day): day is number => day !== null);
  const start = startDate ? dayNumber(startDate) : null;
  const end = dueDate ? dayNumber(dueDate) : datedDays.length > 0 ? Math.max(...datedDays) : null;

  if (start === null || end === null || end <= start) {
    const width = 1 / count;
    return {
      mode: "even",
      segments: stages.map((stage, index) => ({ stage, left: index * width, width, fromDay: 0, toDay: 0 })),
      start: null,
      end: null,
      today: null,
      state: null,
    };
  }

  const edges = edgesOf(stageDays, start, end);
  const widths = withMinimumWidth(edges.slice(1).map((edge, index) => (edge - edges[index]) / (end - start)));

  let left = 0;
  const segments = stages.map((stage, index) => {
    const segment = { stage, left, width: widths[index], fromDay: Math.round(edges[index]), toDay: Math.round(edges[index + 1]) };
    left += widths[index];
    return segment;
  });

  return { mode: "time", segments, start: isoDate(start), end: isoDate(end), ...locateToday(dayNumber(today), edges, segments) };
}

/** `count + 1` day numbers, the first at the start and the last at the end, never going backwards. */
function edgesOf(stageDays: (number | null)[], start: number, end: number): number[] {
  const known: (number | null)[] = [start];
  let previous = start;
  stageDays.forEach((day, index) => {
    if (index === stageDays.length - 1 || day === null) {
      known.push(index === stageDays.length - 1 ? end : null);
      return;
    }
    previous = Math.min(Math.max(day, previous), end);
    known.push(previous);
  });

  // Runs of unknown edges are spread evenly between the known ones on each side.
  const edges = [...known] as number[];
  let from = 0;
  for (let index = 1; index < known.length; index++) {
    if (known[index] === null) {
      continue;
    }
    for (let gap = from + 1; gap < index; gap++) {
      edges[gap] = edges[from] + ((edges[index] - edges[from]) * (gap - from)) / (index - from);
    }
    from = index;
  }
  return edges;
}

/** Widths that sum to 1, none below the minimum; the others give up room in proportion. */
function withMinimumWidth(raw: number[]): number[] {
  const minimum = Math.min(0.08, 0.6 / raw.length);
  const held = raw.map(() => false);
  let widths = raw;

  for (let pass = 0; pass < raw.length; pass++) {
    const heldTotal = held.filter(Boolean).length * minimum;
    const freeRaw = raw.reduce((sum, width, index) => (held[index] ? sum : sum + width), 0);
    const scale = freeRaw > 0 ? (1 - heldTotal) / freeRaw : 0;
    widths = raw.map((width, index) => (held[index] ? minimum : width * scale));

    let changed = false;
    widths.forEach((width, index) => {
      if (!held[index] && width < minimum) {
        held[index] = true;
        changed = true;
      }
    });
    if (!changed) {
      break;
    }
  }

  const total = widths.reduce((sum, width) => sum + width, 0);
  return widths.map((width) => width / total);
}

function locateToday(today: number, edges: number[], segments: TimelineSegment[]): Pick<Timeline, "today" | "state"> {
  if (today < edges[0]) {
    return { today: 0, state: "before" };
  }
  if (today > edges[edges.length - 1]) {
    return { today: 1, state: "over" };
  }

  // The first segment that has not ended yet; inside it, today sits in proportion to the days.
  const index = edges.findIndex((edge, position) => position > 0 && today <= edge) - 1;
  const span = edges[index + 1] - edges[index];
  const share = span > 0 ? (today - edges[index]) / span : 0;
  return { today: segments[index].left + segments[index].width * share, state: "running" };
}
