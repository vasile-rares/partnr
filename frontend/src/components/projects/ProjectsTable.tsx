import Link from "next/link";
import { ProjectProgress } from "@/components/projects/ProjectProgress";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatDate } from "@/lib/format";
import type { Project } from "@/lib/projects/types";

type ProjectsTableProps = {
  workspaceId: string;
  projects: Project[];
  /** False on a client's own page, where every row has the same client. */
  showClient?: boolean;
};

export function ProjectsTable({ workspaceId, projects, showClient = true }: ProjectsTableProps) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Project</TableHead>
          {showClient ? <TableHead>Client</TableHead> : null}
          <TableHead>Status</TableHead>
          <TableHead>Progress</TableHead>
          <TableHead>Due</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {projects.map((project) => (
          <TableRow key={project.id}>
            <TableCell className="font-medium">
              <Link href={`/w/${workspaceId}/projects/${project.id}`} className="hover:underline">
                {project.name}
              </Link>
              {project.archivedAtUtc ? (
                <Badge variant="secondary" className="ml-2">
                  Archived
                </Badge>
              ) : null}
              {project.approvals.changesRequested > 0 ? (
                <Badge variant="destructive" className="ml-2">
                  {project.approvals.changesRequested} changes requested
                </Badge>
              ) : null}
              {project.approvals.pending > 0 ? (
                <Badge variant="secondary" className="ml-2">
                  {project.approvals.pending} waiting for client
                </Badge>
              ) : null}
            </TableCell>
            {showClient ? (
              <TableCell>
                <Link href={`/w/${workspaceId}/clients/${project.client.id}`} className="hover:underline">
                  {project.client.displayName}
                </Link>
              </TableCell>
            ) : null}
            <TableCell>
              <ProjectStatusBadge status={project.status} />
            </TableCell>
            <TableCell>
              <ProjectProgress progress={project.progress} />
            </TableCell>
            <TableCell>{project.dueDate ? formatDate(project.dueDate) : "—"}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
