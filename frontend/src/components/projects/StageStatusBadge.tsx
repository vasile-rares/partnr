import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { stageStatusLabel, type StageStatus } from "@/lib/projects/types";

// The three status colours are the only tokens of our own (see globals.css): done, in progress, still to come.
const dots: Record<StageStatus, string> = {
  ToDo: "bg-status-todo",
  InProgress: "bg-status-doing",
  Done: "bg-status-done",
};

/** A stage's status as a small label. The text carries the meaning; the coloured dot only helps. */
export function StageStatusBadge({ status }: { status: StageStatus }) {
  return (
    <Badge variant="outline" className="gap-1.5">
      <span aria-hidden className={cn("size-2 rounded-full", dots[status])} />
      {stageStatusLabel(status)}
    </Badge>
  );
}
