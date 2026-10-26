import { expect, test } from "@playwright/test";
import { buildTimeline, dayNumber, isoDate, orderByDate } from "@/lib/projects/timeline";
import type { Stage, StageStatus } from "@/lib/projects/types";

// No browser here: the layout rules of the project bar are plain functions of the dates.
let counter = 0;
const stage = (dueDate: string | null, status: StageStatus = "ToDo"): Stage => ({
  id: `stage-${counter++}`,
  name: `Stage ${counter}`,
  description: null,
  dueDate,
  status,
});

const widths = (stages: Stage[], startDate: string | null, dueDate: string | null, today = "2026-01-01") =>
  buildTimeline({ startDate, dueDate, stages }, today).segments.map((segment) => segment.width);

test("no stages leaves nothing to draw", () => {
  const timeline = buildTimeline({ startDate: "2026-01-01", dueDate: "2026-02-01", stages: [] }, "2026-01-10");

  expect(timeline).toMatchObject({ mode: "even", segments: [], today: null, state: null });
});

test("without a start date or an end the stages share the bar evenly and there is no today marker", () => {
  const stages = [stage(null), stage(null), stage(null), stage(null)];

  for (const [start, due] of [
    [null, "2026-03-01"],
    ["2026-01-01", null],
    [null, null],
  ] as const) {
    const timeline = buildTimeline({ startDate: start, dueDate: due, stages }, "2026-01-10");
    expect(timeline.mode).toBe("even");
    expect(timeline.today).toBeNull();
    expect(timeline.segments.map((s) => s.width)).toEqual([0.25, 0.25, 0.25, 0.25]);
  }
});

test("each stage runs from the previous date to its own, and the last one reaches the end", () => {
  const stages = [stage("2026-01-11"), stage("2026-01-31"), stage("2026-02-01")];

  const [first, second, third] = widths(stages, "2026-01-01", "2026-04-11");

  // 10, 20 and 70 of 100 days.
  expect(first).toBeCloseTo(0.1);
  expect(second).toBeCloseTo(0.2);
  expect(third).toBeCloseTo(0.7);
});

test("without a project deadline the bar ends at the latest stage date", () => {
  const timeline = buildTimeline({ startDate: "2026-01-01", dueDate: null, stages: [stage("2026-01-11"), stage("2026-01-21")] }, "2026-01-05");

  expect(timeline.mode).toBe("time");
  expect(timeline.end).toBe("2026-01-21");
  expect(timeline.segments.map((s) => s.width)).toEqual([0.5, 0.5]);
});

test("a stage with no date takes an equal share of the gap between its neighbours", () => {
  const stages = [stage("2026-01-11"), stage(null), stage(null), stage("2026-01-31")];

  const [first, ...middle] = widths(stages, "2026-01-01", "2026-01-31");

  expect(first).toBeCloseTo(1 / 3);
  expect(middle.map((width) => Number(width.toFixed(3)))).toEqual([0.222, 0.222, 0.222]);
});

test("stages given out of date order are drawn by date, and a short stage keeps a minimum width", () => {
  const stages = [stage("2026-01-21"), stage("2026-01-05"), stage("2026-01-21"), stage("2026-02-10")];

  const result = widths(stages, "2026-01-01", "2026-02-10");

  expect(result.reduce((sum, width) => sum + width, 0)).toBeCloseTo(1);
  expect(Math.min(...result)).toBeGreaterThanOrEqual(0.08 - 1e-9);
});

test("a single stage fills the whole bar", () => {
  expect(widths([stage("2026-01-10")], "2026-01-01", "2026-03-01")).toEqual([1]);
});

test("segments sit side by side from the left edge", () => {
  const timeline = buildTimeline({ startDate: "2026-01-01", dueDate: "2026-01-31", stages: [stage("2026-01-11"), stage("2026-01-31")] }, "2026-01-02");

  expect(timeline.segments[0].left).toBe(0);
  expect(timeline.segments[1].left).toBeCloseTo(timeline.segments[0].width);
});

test("today is placed inside the segment it falls in, before the start or after the end it is pinned", () => {
  const input = { startDate: "2026-01-01", dueDate: "2026-01-21", stages: [stage("2026-01-11"), stage("2026-01-21")] };

  const middle = buildTimeline(input, "2026-01-06");
  const before = buildTimeline(input, "2025-12-01");
  const after = buildTimeline(input, "2026-03-01");
  const onTheDeadline = buildTimeline(input, "2026-01-21");

  expect(middle).toMatchObject({ state: "running" });
  expect(middle.today).toBeCloseTo(0.25);
  expect(before).toMatchObject({ today: 0, state: "before" });
  expect(after).toMatchObject({ today: 1, state: "over" });
  expect(onTheDeadline).toMatchObject({ today: 1, state: "running" });
});

test("each segment knows the days it spans, so a position on the bar can become a date again", () => {
  const stages = [stage("2026-01-11"), stage(null), stage("2026-01-31")];

  const { segments } = buildTimeline({ startDate: "2026-01-01", dueDate: "2026-01-31", stages }, "2026-01-05");

  expect(isoDate(segments[0].fromDay)).toBe("2026-01-01");
  expect(segments[1].fromDay).toBe(segments[0].toDay);
  expect(segments[2].fromDay).toBe(segments[1].toDay);
  expect(isoDate(segments[2].toDay)).toBe("2026-01-31");
  expect(isoDate(dayNumber("2026-03-01"))).toBe("2026-03-01");
});

test("stages are ordered by date, and one without a date keeps its place in the list", () => {
  const [a, b, c, d] = [stage("2026-03-01"), stage(null), stage("2026-01-01"), stage("2026-02-01")];

  expect(orderByDate([a, b, c, d])).toEqual([c, b, d, a]);
  expect(orderByDate([a, c])).toEqual([c, a]);
  expect(orderByDate([b])).toEqual([b]);
  const timeline = buildTimeline({ startDate: "2025-12-01", dueDate: "2026-03-01", stages: [a, b, c, d] }, "2026-01-05");
  expect(timeline.segments.map((segment) => segment.stage)).toEqual([c, b, d, a]);
});
