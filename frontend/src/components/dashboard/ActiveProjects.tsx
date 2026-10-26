import Link from "next/link";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { formatDate } from "@/lib/format";
import type { Project } from "@/lib/projects/types";

export function ActiveProjects({ workspaceId, projects }: { workspaceId: string; projects: Project[] }) {
  return (
    <section aria-labelledby="active-projects-title">
      <div className="mb-4 flex items-baseline justify-between gap-3">
        <h2 id="active-projects-title" className="font-heading text-xl font-semibold">
          Active projects
        </h2>
        <Link href={`/w/${workspaceId}/projects`} className="text-sm text-muted-foreground hover:underline">
          All projects
        </Link>
      </div>
      {projects.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No project is running. Move one out of Planning when it is ready for your client to see.
        </p>
      ) : (
        <ul className="flex flex-col divide-y rounded-lg border">
          {projects.map((project) => (
            <li key={project.id} className="flex flex-wrap items-center justify-between gap-x-6 gap-y-2 p-4">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <Link href={`/w/${workspaceId}/projects/${project.id}`} className="font-medium hover:underline">
                    {project.name}
                  </Link>
                  {project.status === "OnHold" ? <ProjectStatusBadge status={project.status} /> : null}
                </div>
                <p className="text-sm text-muted-foreground">
                  {project.client.displayName}
                  {project.dueDate ? ` · Due ${formatDate(project.dueDate)}` : null}
                </p>
              </div>
              <ProjectProgress progress={project.progress} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
