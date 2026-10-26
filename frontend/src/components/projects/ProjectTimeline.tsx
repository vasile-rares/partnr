import { CheckIcon, CircleDotIcon, CircleIcon } from "lucide-react";
import type { ReactNode } from "react";
import { AddStageDialog } from "@/components/projects/AddStageDialog";
import { StageDateHandle } from "@/components/projects/StageDateHandle";
import { StagePopover } from "@/components/projects/StagePopover";
import { daysUntil, formatDate, formatDayMonth } from "@/lib/format";
import { buildTimeline, todayUtc } from "@/lib/projects/timeline";
import { stageStatusLabel, type ProjectStatus, type Stage, type StageStatus } from "@/lib/projects/types";
import { cn } from "@/lib/utils";

type ProjectTimelineProps = {
  project: {
    status: ProjectStatus;
    startDate: string | null;
    dueDate: string | null;
    stages: Stage[];
    progress: { done: number; total: number };
  };
  /** The freelancer on a project that can still change; without it the stages can only be read. */
  editable?: { workspaceId: string; projectId: string };
  /** What leads to setting the dates, shown when the project has none. */
  datesAction?: ReactNode;
};

const barColors: Record<StageStatus, string> = {
  Done: "bg-status-done",
  InProgress: "bg-status-doing",
  ToDo: "bg-status-todo/30",
};

// The label carries the status too, so the bar is not read by colour alone.
const labelIcons: Record<StageStatus, { Icon: typeof CheckIcon; color: string }> = {
  Done: { Icon: CheckIcon, color: "text-status-done" },
  InProgress: { Icon: CircleDotIcon, color: "text-status-doing" },
  ToDo: { Icon: CircleIcon, color: "text-muted-foreground" },
};

/** The whole project on one thin bar: each stage a segment laid out by its dates, today marked, the deadline beside it. */
export function ProjectTimeline({ project, editable, datesAction }: ProjectTimelineProps) {
  const timeline = buildTimeline(project, todayUtc());
  const { done, total } = project.progress;
  const completed = project.status === "Completed";
  const ordered = timeline.segments.map(({ stage }) => stage);
  const current = ordered.find((stage) => stage.status === "InProgress") ?? ordered.find((stage) => stage.status === "ToDo");
  const today = completed ? null : timeline.today;
  const limits = { min: project.startDate, max: project.dueDate };
  const ranges = timeline.segments.map(({ fromDay, toDay }) => ({ fromDay, toDay }));

  return (
    <section aria-label="Timeline" className="group/bar mb-8">
      <div className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1">
        {project.dueDate ? <Remaining dueDate={project.dueDate} completed={completed} /> : completed ? <p className="text-sm font-medium">Completed</p> : null}
        <p className="ml-auto flex flex-wrap items-baseline justify-end gap-x-3 text-sm">
          <span className="font-medium">{total === 0 ? "No stages yet" : `${done} of ${total} done`}</span>
          <span className="text-muted-foreground">
            {project.startDate || project.dueDate ? (
              dateRange(project.startDate, project.dueDate)
            ) : (
              <>No dates yet. {datesAction}</>
            )}
          </span>
        </p>
      </div>

      {total === 0 ? (
        <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2">
          <p className="text-sm text-muted-foreground">
            {editable ? "Break the project into steps your client can follow." : "Stages show up here once they are planned."}
          </p>
          {editable ? <AddStageDialog workspaceId={editable.workspaceId} projectId={editable.projectId} variant="button" limits={limits} /> : null}
        </div>
      ) : (
        <>
          <div className={editable ? "pr-8" : undefined}>
            <div className={cn("relative", today !== null ? "mt-7" : "mt-3", project.status === "OnHold" && "opacity-60")}>
              <ol className="flex gap-1">
                {timeline.segments.map(({ stage, width }, index) => (
                  <li key={stage.id} style={{ flex: `${width} 1 0%` }} className="relative min-w-0">
                    <StagePopover
                      stage={stage}
                      editable={editable}
                      limits={limits}
                      label={`${stage.name}, ${stageStatusLabel(stage.status)}${stage.dueDate ? `, due ${formatDate(stage.dueDate)}` : ""}`}
                      className="group flex h-5 w-full items-center rounded-full"
                    >
                      <span className={cn("h-2 w-full rounded-full transition-[height] group-hover:h-2.5", barColors[stage.status])} />
                    </StagePopover>
                    {editable && timeline.mode === "time" && index < total - 1 ? (
                      <StageDateHandle
                        workspaceId={editable.workspaceId}
                        projectId={editable.projectId}
                        stage={stage}
                        index={index}
                        ranges={ranges}
                      />
                    ) : null}
                  </li>
                ))}
              </ol>
              {today !== null ? <TodayMarker position={today} late={timeline.state === "over"} /> : null}
              {editable ? (
                <div className="absolute top-1/2 -right-8 -translate-y-1/2">
                  <AddStageDialog workspaceId={editable.workspaceId} projectId={editable.projectId} variant="icon" limits={limits} />
                </div>
              ) : null}
            </div>

            <ol aria-hidden className="mt-1 hidden gap-1 sm:flex">
              {timeline.segments.map(({ stage, width }) => {
                const { Icon, color } = labelIcons[stage.status];
                const late = stage.dueDate !== null && stage.status !== "Done" && daysUntil(stage.dueDate) < 0;
                return (
                  <li key={stage.id} style={{ flex: `${width} 1 0%` }} className="min-w-0">
                    <p className={cn("flex items-center gap-1 text-xs", stage.id === current?.id ? "font-medium" : "text-muted-foreground")}>
                      <Icon className={cn("size-3 shrink-0", color)} />
                      <span className="truncate">{stage.name}</span>
                    </p>
                    <p className={cn("truncate pl-4 text-xs text-muted-foreground/70", late && "text-destructive")}>
                      {stage.dueDate ? formatDayMonth(stage.dueDate) : "No date"}
                    </p>
                  </li>
                );
              })}
            </ol>
          </div>
          {current ? (
            <p className="mt-3 text-sm sm:hidden">
              <span className="text-muted-foreground">{current.status === "InProgress" ? "Now:" : "Next:"}</span> {current.name}
            </p>
          ) : null}
        </>
      )}
    </section>
  );
}

/** "6 Oct – 5 Dec 2026"; the year is written once when both dates share it. */
function dateRange(start: string | null, due: string | null): string {
  if (start && due) {
    const sameYear = start.slice(0, 4) === due.slice(0, 4);
    return `${sameYear ? formatDayMonth(start) : formatDate(start)} – ${formatDate(due)}`;
  }
  return start ? `Started ${formatDate(start)}` : `Due ${formatDate(due!)}`;
}

function TodayMarker({ position, late }: { position: number; late: boolean }) {
  return (
    <div
      aria-hidden
      className={cn("pointer-events-none absolute top-1/2 h-4 w-[3px] -translate-x-1/2 -translate-y-1/2 rounded-full", late ? "bg-destructive" : "bg-foreground")}
      style={{ left: `${position * 100}%` }}
    >
      <span
        className={cn(
          "absolute -top-4 left-1/2 -translate-x-1/2 text-[10px] font-medium whitespace-nowrap",
          late ? "text-destructive" : "text-muted-foreground",
        )}
      >
        Today
      </span>
    </div>
  );
}

function Remaining({ dueDate, completed }: { dueDate: string; completed: boolean }) {
  if (completed) {
    return <p className="text-sm font-medium">Completed</p>;
  }

  const days = daysUntil(dueDate);
  if (days < 0) {
    return <p className="text-sm font-medium text-destructive">{days === -1 ? "1 day overdue" : `${-days} days overdue`}</p>;
  }
  if (days === 0) {
    return <p className="text-sm font-medium">Due today</p>;
  }
  return <p className="text-sm font-medium">{days === 1 ? "1 day left" : `${days} days left`}</p>;
}
