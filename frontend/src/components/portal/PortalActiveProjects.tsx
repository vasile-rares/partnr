import Link from "next/link";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { formatDate } from "@/lib/format";
import type { PortalProject } from "@/lib/portal/types";
import { summarize } from "@/lib/portal/summary";

export function PortalActiveProjects({ workspaceId, projects }: { workspaceId: string; projects: PortalProject[] }) {
  return (
    <section aria-labelledby="active-title">
      <div className="mb-4 flex items-baseline justify-between gap-3">
        <h2 id="active-title" className="font-heading text-xl font-semibold">
          Active projects
        </h2>
        <Link href={`/portal/${workspaceId}/projects`} className="text-sm text-muted-foreground hover:underline">
          All projects
        </Link>
      </div>
      {projects.length === 0 ? (
        <p className="text-sm text-muted-foreground">No project is running at the moment.</p>
      ) : (
        <ul className="flex flex-col divide-y rounded-lg border">
          {projects.map((project) => {
            const { now, next } = summarize(project.status, project.stages);
            return (
              <li key={project.id}>
                <Link
                  href={`/portal/${workspaceId}/projects/${project.id}`}
                  className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 p-4 hover:bg-muted/50"
                >
                  <div className="min-w-0">
                    <p className="font-medium">{project.name}</p>
                    <p className="mt-1 text-sm text-muted-foreground">{now}</p>
                    {next ? <p className="text-sm text-muted-foreground">Up next: {next}</p> : null}
                    {project.dueDate ? <p className="text-sm text-muted-foreground">Due {formatDate(project.dueDate)}</p> : null}
                  </div>
                  <ProjectProgress progress={project.progress} />
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
