import Link from "next/link";
import { DividedList } from "@/components/layout/DividedList";
import { Section } from "@/components/layout/Section";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { formatDate } from "@/lib/format";
import type { Project } from "@/lib/projects/types";

export function FirmActiveProjects({ workspaceId, projects }: { workspaceId: string; projects: Project[] }) {
  return (
    <Section
      title="Active projects"
      className="active-projects"
      action={
        <Link href={`/w/${workspaceId}/projects`} className="text-sm text-muted-foreground hover:underline">
          All projects
        </Link>
      }
    >
      {projects.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No project is running. Move one out of Planning when it is ready for your client to see.
        </p>
      ) : (
        <DividedList className="active-projects-list">
          {projects.map((project) => (
            <li key={project.id} className="active-projects-item flex flex-wrap items-center justify-between gap-x-6 gap-y-2 p-4">
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
        </DividedList>
      )}
    </Section>
  );
}
