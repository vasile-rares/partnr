import Link from "next/link";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { Badge } from "@/components/ui/badge";
import { formatDate } from "@/lib/format";
import type { PortalProjectSummary } from "@/lib/portal/types";

/** The projects one freelancer or agency shared with the signed-in client. */
export function PortalProjectList({ workspaceId, projects }: { workspaceId: string; projects: PortalProjectSummary[] }) {
  if (projects.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        Nothing has been shared with you yet. Projects show up here as soon as they are ready.
      </p>
    );
  }

  return (
    <ul className="divide-y rounded-lg border">
      {projects.map((project) => (
        <li key={project.id}>
          <Link
            href={`/portal/${workspaceId}/projects/${project.id}`}
            className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 p-4 hover:bg-muted/50"
          >
            <div className="min-w-0">
              <p className="font-medium">{project.name}</p>
              <p className="mt-1 text-sm text-muted-foreground">
                {project.dueDate ? `Due ${formatDate(project.dueDate)}` : "No due date"}
              </p>
            </div>
            <div className="flex items-center gap-4">
              {project.awaitingApproval > 0 ? (
                <Badge variant="secondary">{project.awaitingApproval} waiting for you</Badge>
              ) : null}
              <ProjectProgress progress={project.progress} />
              <ProjectStatusBadge status={project.status} />
            </div>
          </Link>
        </li>
      ))}
    </ul>
  );
}
