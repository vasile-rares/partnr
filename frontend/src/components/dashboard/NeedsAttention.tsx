import { ChevronRightIcon } from "lucide-react";
import Link from "next/link";
import type { Project } from "@/lib/projects/types";

/** Projects where the client answered with changes (the freelancer's move) or has not answered yet; hidden when there are none. */
export function NeedsAttention({ workspaceId, projects }: { workspaceId: string; projects: Project[] }) {
  if (projects.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="attention-title">
      <h2 id="attention-title" className="mb-4 font-heading text-xl font-semibold">
        Needs your attention
      </h2>
      <ul className="flex flex-col divide-y rounded-lg border">
        {projects.map((project) => {
          const { pending, changesRequested } = project.approvals;
          return (
            <li key={project.id}>
              <Link
                href={`/w/${workspaceId}/projects/${project.id}?tab=deliverables`}
                className="flex items-center justify-between gap-4 p-4 hover:bg-muted/50"
              >
                <div className="min-w-0">
                  <p className="font-medium">{project.name}</p>
                  <p className="text-sm text-muted-foreground">{project.client.displayName}</p>
                  <p className="mt-1 text-sm">
                    {changesRequested > 0 ? (
                      <span className="font-medium text-destructive">{changesRequested} with changes requested</span>
                    ) : null}
                    {changesRequested > 0 && pending > 0 ? " · " : null}
                    {pending > 0 ? <span className="text-muted-foreground">{pending} waiting for the client</span> : null}
                  </p>
                </div>
                <ChevronRightIcon aria-hidden className="size-4 shrink-0 text-muted-foreground" />
              </Link>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
