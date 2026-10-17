import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { stageStatusLabel, type StageStatus } from "@/lib/projects/types";

const dots: Record<StageStatus, string> = {
  ToDo: "bg-status-todo",
  InProgress: "bg-status-doing",
  Done: "bg-status-done",
};

export function StageStatusBadge({ status }: { status: StageStatus }) {
  return (
    <Badge variant="outline" className="gap-1.5">
      <span aria-hidden className={cn("size-2 rounded-full", dots[status])} />
      {stageStatusLabel(status)}
    </Badge>
  );
}
