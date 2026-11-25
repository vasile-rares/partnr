import { orderByDate } from "@/lib/projects/timeline";
import type { Stage, StageStatus } from "@/lib/projects/types";
import { cn } from "@/lib/utils";

export const stageColors: Record<StageStatus, string> = {
  Done: "bg-status-done",
  InProgress: "bg-status-doing",
  ToDo: "bg-status-todo/30",
};

/** One segment per stage, in the order of the project bar, with what is in progress picked out. No text, so it lines up row after row. */
export function StageSegments({ stages }: { stages: Stage[] }) {
  if (stages.length === 0) {
    return <span className="text-muted-foreground">No stages yet</span>;
  }

  const done = stages.filter((stage) => stage.status === "Done").length;
  const doing = stages.filter((stage) => stage.status === "InProgress").length;
  const label = `${done} of ${stages.length} stages done${doing > 0 ? `, ${doing} in progress` : ""}`;

  return (
    <div role="img" aria-label={label} title={label} className="stage-segments flex min-w-28 max-w-40 gap-0.5">
      {orderByDate(stages).map((stage) => (
        <span key={stage.id} className={cn("h-1.5 flex-1 rounded-full", stageColors[stage.status])} />
      ))}
    </div>
  );
}
