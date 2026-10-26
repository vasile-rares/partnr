"use client";

import { useEffect, useRef, useState, type KeyboardEvent, type PointerEvent } from "react";
import { useStageRequest } from "@/components/projects/useStageRequest";
import { formatDate, formatDayMonth } from "@/lib/format";
import { isoDate } from "@/lib/projects/timeline";
import type { Stage } from "@/lib/projects/types";
import { cn } from "@/lib/utils";

export type DayRange = { fromDay: number; toDay: number };

type StageDateHandleProps = {
  workspaceId: string;
  projectId: string;
  stage: Stage;
  /** This stage's place among the bar's segments, and the days every segment spans. */
  index: number;
  ranges: DayRange[];
};

// A handle moved but not saved yet. It only counts while the stage still ends on the day it started from, so once the
// saved date comes back from the server the preview lets go on its own.
type Preview = { base: number; day: number; shift: number };

const KEY_COMMIT_DELAY_MS = 700;

const stageColors: Record<Stage["status"], string> = {
  Done: "var(--status-done)",
  InProgress: "var(--status-doing)",
  ToDo: "var(--status-todo)",
};

/**
 * The end of a stage on the bar, which can be dragged (or moved with the arrow keys) to change the stage's due date.
 * It sits on the edge between this segment and the next, and cannot go past either neighbour's own dates.
 */
export function StageDateHandle({ workspaceId, projectId, stage, index, ranges }: StageDateHandleProps) {
  const handle = useRef<HTMLDivElement>(null);
  const dragging = useRef(false);
  // Where the pointer took hold and where this stage's edge was then: the drag moves the edge by as much as the pointer
  // moved, so pressing the handle without moving never changes the date.
  const grab = useRef({ pointerX: 0, edgeX: 0 });
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const [preview, setPreview] = useState<Preview | null>(null);
  const { run } = useStageRequest(workspaceId, projectId);

  const restDay = ranges[index].toDay;
  const minDay = ranges[index].fromDay;
  const maxDay = ranges[index + 1].toDay;
  const shown = preview !== null && preview.base === restDay ? preview : null;
  const day = shown?.day ?? restDay;

  useEffect(() => () => clearTimeout(timer.current), []);

  const clamp = (value: number) => Math.min(maxDay, Math.max(minDay, value));
  const segmentRects = () => Array.from(handle.current?.closest("ol")?.children ?? []).map((item) => item.getBoundingClientRect());

  function dayAt(pointerX: number): number {
    const rects = segmentRects();
    const x = grab.current.edgeX + pointerX - grab.current.pointerX;
    const k = Math.max(0, rects.findIndex((rect, position) => position === rects.length - 1 || x < rects[position + 1].left));
    const rect = rects[k];
    const share = rect.width > 0 ? Math.min(1, Math.max(0, (x - rect.left) / rect.width)) : 0;
    return clamp(Math.round(ranges[k].fromDay + share * (ranges[k].toDay - ranges[k].fromDay)));
  }

  /** How far the handle has to move from where the stage ends now to sit on the given day. */
  function shiftTo(target: number): number {
    const rects = segmentRects();
    const k = Math.max(0, ranges.findIndex((range) => target <= range.toDay));
    const span = ranges[k].toDay - ranges[k].fromDay;
    const share = span > 0 ? (target - ranges[k].fromDay) / span : 1;
    return rects[k].left + share * rects[k].width - rects[index].right;
  }

  function show(target: number) {
    setPreview({ base: restDay, day: target, shift: shiftTo(target) });
  }

  function commit(target: number) {
    if (target === restDay) {
      setPreview(null);
      return;
    }
    show(target);
    run(
      "PUT",
      `/${stage.id}`,
      { name: stage.name, description: stage.description, dueDate: isoDate(target), status: stage.status },
      undefined,
      () => setPreview(null),
    );
  }

  function onPointerDown(event: PointerEvent<HTMLDivElement>) {
    if (event.button !== 0) {
      return;
    }
    event.currentTarget.setPointerCapture(event.pointerId);
    dragging.current = true;
    grab.current = { pointerX: event.clientX, edgeX: segmentRects()[index].right };
    clearTimeout(timer.current);
    show(day);
  }

  function onPointerMove(event: PointerEvent<HTMLDivElement>) {
    if (dragging.current) {
      show(dayAt(event.clientX));
    }
  }

  function onPointerUp(event: PointerEvent<HTMLDivElement>) {
    if (!dragging.current) {
      return;
    }
    dragging.current = false;
    commit(dayAt(event.clientX));
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const direction = event.key === "ArrowRight" || event.key === "ArrowUp" ? 1 : event.key === "ArrowLeft" || event.key === "ArrowDown" ? -1 : 0;
    if (direction === 0) {
      return;
    }
    event.preventDefault();
    const target = clamp(day + direction * (event.shiftKey ? 7 : 1));
    show(target);
    clearTimeout(timer.current);
    timer.current = setTimeout(() => commit(target), KEY_COMMIT_DELAY_MS);
  }

  const change = day - restDay;
  const shift = shown?.shift ?? 0;
  const ghostColor = stageColors[stage.status];

  return (
    <>
      {shift !== 0 ? (
        // The part the stage gains (hatched, past its end) or loses (hatched, over its tail) if the handle is let go here.
        <span
          aria-hidden
          className="pointer-events-none absolute top-1/2 z-[5] h-2 -translate-y-1/2 rounded-full border border-dashed"
          style={{
            ...(shift > 0 ? { left: "100%" } : { right: 0 }),
            width: Math.abs(shift),
            borderColor: ghostColor,
            backgroundColor: "color-mix(in srgb, var(--background) 60%, transparent)",
            backgroundImage: `repeating-linear-gradient(45deg, color-mix(in srgb, ${ghostColor} 55%, transparent) 0 4px, transparent 4px 8px)`,
          }}
        />
      ) : null}
      <div
        ref={handle}
        role="slider"
        tabIndex={0}
        aria-label={`Due date of ${stage.name}`}
        aria-orientation="horizontal"
        aria-valuemin={minDay}
        aria-valuemax={maxDay}
        aria-valuenow={day}
        aria-valuetext={formatDate(isoDate(day))}
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={() => {
          dragging.current = false;
          setPreview(null);
        }}
        onKeyDown={onKeyDown}
        style={{ translate: `${shift}px -50%` }}
        className={cn(
          "group/handle absolute top-1/2 -right-[10px] z-10 flex h-7 w-4 cursor-col-resize touch-none items-center justify-center outline-none",
          "opacity-0 transition-opacity group-hover/bar:opacity-100 focus-visible:opacity-100 [@media(hover:none)]:opacity-100",
          shown && "opacity-100",
        )}
      >
        <span className="h-6 w-2 rounded-full bg-foreground shadow-sm group-focus-visible/handle:ring-2 group-focus-visible/handle:ring-ring group-focus-visible/handle:ring-offset-2 group-focus-visible/handle:ring-offset-background" />
        {shown ? (
          <span
            role="status"
            className="pointer-events-none absolute -top-8 left-1/2 -translate-x-1/2 rounded-md bg-foreground px-2 py-1 text-xs whitespace-nowrap text-background"
          >
            {stage.name} · due {formatDayMonth(isoDate(day))}
            {change !== 0 ? ` (${change > 0 ? "+" : "−"}${Math.abs(change)} ${Math.abs(change) === 1 ? "day" : "days"})` : ""}
          </span>
        ) : null}
      </div>
    </>
  );
}
