import type { Progress } from "@/lib/projects/types";

/** "2 of 4 done" with a thin bar. A project with no stages yet says so instead of showing an empty bar. */
export function ProjectProgress({ progress }: { progress: Progress }) {
  if (progress.total === 0) {
    return <span className="text-muted-foreground">No stages yet</span>;
  }

  return (
    <div className="flex min-w-28 flex-col gap-1">
      <div aria-hidden className="h-1.5 overflow-hidden rounded-full bg-muted">
        <div className="h-full rounded-full bg-status-done" style={{ width: `${(progress.done / progress.total) * 100}%` }} />
      </div>
      <span className="text-xs text-muted-foreground">
        {progress.done} of {progress.total} done
      </span>
    </div>
  );
}
