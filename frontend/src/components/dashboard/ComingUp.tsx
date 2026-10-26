import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { UPCOMING_DAYS, type Deadline } from "@/lib/dashboard/deadlines";
import { dueIn, formatDate } from "@/lib/format";

export function ComingUp({ workspaceId, deadlines }: { workspaceId: string; deadlines: Deadline[] }) {
  return (
    <section aria-labelledby="coming-up-title">
      <h2 id="coming-up-title" className="mb-4 font-heading text-xl font-semibold">
        Coming up
      </h2>
      {deadlines.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nothing is due in the next {UPCOMING_DAYS} days.</p>
      ) : (
        <ul className="flex flex-col divide-y rounded-lg border">
          {deadlines.map((deadline) => {
            const late = deadline.daysLeft < 0;
            return (
              <li key={deadline.key} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 p-4">
                <div className="min-w-0">
                  <Link href={`/w/${workspaceId}/projects/${deadline.project.id}`} className="font-medium hover:underline">
                    {deadline.title}
                  </Link>
                  <p className="text-sm text-muted-foreground">
                    {deadline.kind === "stage" ? `Stage of ${deadline.project.name} · ` : "Project deadline · "}
                    {deadline.clientName}
                  </p>
                </div>
                <div className="flex items-center gap-2 text-sm">
                  {late ? <Badge variant="destructive">Overdue</Badge> : null}
                  <span className={late ? "text-destructive" : "text-muted-foreground"}>
                    {dueIn(deadline.daysLeft)} · <time dateTime={deadline.date}>{formatDate(deadline.date)}</time>
                  </span>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
