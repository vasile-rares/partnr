import type { Progress } from "@/lib/projects/types";
import { cn } from "@/lib/utils";

// More segments than this would be slivers, so a long project gets a continuous bar.
const MAX_SEGMENTS = 12;

/** "2 of 4 done" under one segment per stage. A project with no stages yet says so instead of showing an empty bar. */
export function ProjectProgress({ progress }: { progress: Progress }) {
  if (progress.total === 0) {
    return <span className="text-muted-foreground">No stages yet</span>;
  }

  return (
    <div className="flex min-w-28 flex-col gap-1">
      {progress.total <= MAX_SEGMENTS ? (
        <div aria-hidden className="flex gap-0.5">
          {Array.from({ length: progress.total }, (_, index) => (
            <span key={index} className={cn("h-1.5 flex-1 rounded-full", index < progress.done ? "bg-status-done" : "bg-muted")} />
          ))}
        </div>
      ) : (
        <div aria-hidden className="h-1.5 overflow-hidden rounded-full bg-muted">
          <div className="h-full rounded-full bg-status-done" style={{ width: `${(progress.done / progress.total) * 100}%` }} />
        </div>
      )}
      <span className="text-xs text-muted-foreground">
        {progress.done} of {progress.total} done
      </span>
    </div>
  );
}
