import Link from "next/link";
import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { formatDate } from "@/lib/format";
import { standing } from "@/lib/projects/summary";
import type { ClientProject } from "@/lib/projects/types";

export function ClientActiveProjects({ workspaceId, projects }: { workspaceId: string; projects: ClientProject[] }) {
  return (
    <Section
      title="Active projects"
      className="client-active-projects"
      action={
        <Link href={`/w/${workspaceId}/projects`} className="text-sm text-muted-foreground hover:underline">
          All projects
        </Link>
      }
    >
      {projects.length === 0 ? (
        <p className="text-sm text-muted-foreground">No project is running at the moment.</p>
      ) : (
        <DividedList className="client-active-projects-list">
          {projects.map((project) => {
            const { now, next } = standing(project.status, project.stages);
            return (
              <li key={project.id} className="client-active-projects-item">
                <Link
                  href={`/w/${workspaceId}/projects/${project.id}`}
                  className="client-active-projects-link flex flex-wrap items-center justify-between gap-x-6 gap-y-3 p-4 hover:bg-muted/50"
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
        </DividedList>
      )}
    </Section>
  );
}
