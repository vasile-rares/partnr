import Link from "next/link";
import { DividedList } from "@/components/layout/DividedList";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { Badge } from "@/components/reui/badge";
import { formatDate } from "@/lib/format";
import type { ClientProjectSummary } from "@/lib/projects/types";

/** The projects one workspace shared with the signed-in client. */
export function ClientProjectList({ workspaceId, projects }: { workspaceId: string; projects: ClientProjectSummary[] }) {
  if (projects.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        Nothing has been shared with you yet. Projects show up here as soon as they are ready.
      </p>
    );
  }

  return (
    <DividedList className="client-project-list">
      {projects.map((project) => (
        <li key={project.id} className="client-project-item">
          <Link
            href={`/w/${workspaceId}/projects/${project.id}`}
            className="client-project-link flex flex-wrap items-center justify-between gap-x-6 gap-y-3 p-4 hover:bg-muted/50"
          >
            <div className="min-w-0">
              <p className="font-medium">{project.name}</p>
              <p className="mt-1 text-sm text-muted-foreground">
                {project.dueDate ? `Due ${formatDate(project.dueDate)}` : "No due date"}
              </p>
            </div>
            <div className="client-project-status flex items-center gap-4">
              {project.awaitingApproval > 0 ? (
                <Badge variant="secondary">{project.awaitingApproval} waiting for you</Badge>
              ) : null}
              <ProjectProgress progress={project.progress} />
              <ProjectStatusBadge status={project.status} />
            </div>
          </Link>
        </li>
      ))}
    </DividedList>
  );
}
