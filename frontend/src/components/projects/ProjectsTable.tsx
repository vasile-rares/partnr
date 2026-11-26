"use client";

import { ArrowDownIcon, ArrowUpIcon, ChevronsUpDownIcon, FolderIcon } from "lucide-react";
import Link from "next/link";
import { useMemo, useState } from "react";
import { ClientAvatar } from "@/components/clients/ClientAvatar";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { StageSegments } from "@/components/projects/StageSegments";
import { Badge } from "@/components/reui/badge";
import { IconTile } from "@/components/reui/icon-tile";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatDateInYear } from "@/lib/format";
import type { Project, ProjectStatus } from "@/lib/projects/types";
import { cn } from "@/lib/utils";

type SortKey = "project" | "status" | "progress" | "client" | "due";
type Sort = { key: SortKey; direction: "asc" | "desc" } | null;

// The order a project goes through, not the alphabet.
const statusOrder: ProjectStatus[] = ["Planning", "Active", "OnHold", "Completed"];

const progressRatio = ({ done, total }: Project["progress"]) => (total === 0 ? -1 : done / total);

const compare: Record<SortKey, (a: Project, b: Project) => number> = {
  project: (a, b) => a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: "base" }),
  status: (a, b) => statusOrder.indexOf(a.status) - statusOrder.indexOf(b.status),
  progress: (a, b) => progressRatio(a.progress) - progressRatio(b.progress),
  client: (a, b) => a.client.displayName.localeCompare(b.client.displayName, undefined, { numeric: true, sensitivity: "base" }),
  due: (a, b) => (a.dueDate ?? "").localeCompare(b.dueDate ?? ""),
};

/** Without a sort the rows keep the order they came in. A project with no due date goes last either way round. */
function sortProjects(projects: Project[], sort: Sort): Project[] {
  if (!sort) {
    return projects;
  }
  const sign = sort.direction === "asc" ? 1 : -1;
  return projects.toSorted((a, b) => {
    if (sort.key === "due" && (a.dueDate === null || b.dueDate === null)) {
      return a.dueDate === b.dueDate ? 0 : a.dueDate === null ? 1 : -1;
    }
    return sign * compare[sort.key](a, b);
  });
}

type SortHeadProps = { label: string; sortKey: SortKey; sort: Sort; onSort: (key: SortKey) => void; className?: string; alignEnd?: boolean };

function SortHead({ label, sortKey, sort, onSort, className, alignEnd }: SortHeadProps) {
  const direction = sort?.key === sortKey ? sort.direction : null;
  const Icon = direction === "asc" ? ArrowUpIcon : direction === "desc" ? ArrowDownIcon : ChevronsUpDownIcon;

  return (
    <TableHead aria-sort={direction === "asc" ? "ascending" : direction === "desc" ? "descending" : "none"} className={cn(className, alignEnd && "text-right")}>
      <button
        type="button"
        onClick={() => onSort(sortKey)}
        className={cn(
          "projects-table-sort inline-flex h-7 items-center gap-1 rounded-md px-2 font-medium transition-colors hover:bg-muted focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none",
          alignEnd ? "-mr-2" : "-ml-2",
        )}
      >
        {label}
        <Icon aria-hidden className={cn("size-3.5", direction ? "text-foreground" : "text-muted-foreground")} />
      </button>
    </TableHead>
  );
}

// Widths are shares of the table, so the free space is split between the columns instead of all going to Project and
// crowding the rest against the right edge. They add up to 100 with or without the client column.
const widths = {
  withClient: { project: "w-[36%]", status: "w-[15%]", progress: "w-[21%]", client: "w-[12%]", due: "w-[16%]" },
  withoutClient: { project: "w-[42%]", status: "w-[17%]", progress: "w-[24%]", client: "", due: "w-[17%]" },
};

type ProjectsTableProps = {
  workspaceId: string;
  projects: Project[];
  /** False on a client's own page, where every row has the same client. */
  showClient?: boolean;
};

export function ProjectsTable({ workspaceId, projects, showClient = true }: ProjectsTableProps) {
  const width = showClient ? widths.withClient : widths.withoutClient;
  const [sort, setSort] = useState<Sort>(null);
  const rows = useMemo(() => sortProjects(projects, sort), [projects, sort]);

  // First click ascending, second descending, third back to the original order.
  function sortBy(key: SortKey) {
    setSort((current) => {
      if (current?.key !== key) {
        return { key, direction: "asc" };
      }
      return current.direction === "asc" ? { key, direction: "desc" } : null;
    });
  }

  return (
    <Table className="projects-table">
      <TableHeader>
        <TableRow className="hover:bg-transparent">
          <SortHead label="Project" sortKey="project" sort={sort} onSort={sortBy} className={width.project} />
          <SortHead label="Status" sortKey="status" sort={sort} onSort={sortBy} className={width.status} />
          <SortHead label="Progress" sortKey="progress" sort={sort} onSort={sortBy} className={width.progress} />
          {showClient ? <SortHead label="Client" sortKey="client" sort={sort} onSort={sortBy} className={width.client} /> : null}
          <SortHead label="Due" sortKey="due" sort={sort} onSort={sortBy} className={width.due} alignEnd />
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.map((project) => (
          // relative: the name's link stretches over the whole row (its ::after), so a click anywhere opens the project.
          <TableRow key={project.id} className="projects-table-row relative focus-within:bg-muted/50">
            {/* max-w-0: the column keeps its share of the table, so a long name wraps and the description is cut instead of widening it. */}
            <TableCell className="max-w-0 whitespace-normal">
              <div className="projects-table-project flex items-center gap-3">
                <IconTile variant="elevated" size="sm" aria-hidden>
                  <FolderIcon />
                </IconTile>
                <div className="projects-table-project-info flex min-w-0 flex-col">
                  <div className="text-sm font-medium">
                    <Link href={`/w/${workspaceId}/projects/${project.id}`} className="after:absolute after:inset-0">
                      {project.name}
                    </Link>
                    {project.archivedAtUtc ? (
                      <Badge variant="secondary" className="ml-2">
                        Archived
                      </Badge>
                    ) : null}
                    {project.approvals.changesRequested > 0 ? (
                      <Badge variant="destructive-light" className="ml-2">
                        {project.approvals.changesRequested > 1 ? `${project.approvals.changesRequested} changes requested` : "Changes requested"}
                      </Badge>
                    ) : null}
                  </div>
                  {project.description ? <p className="truncate text-xs text-muted-foreground">{project.description}</p> : null}
                </div>
              </div>
            </TableCell>
            <TableCell>
              <ProjectStatusBadge status={project.status} />
            </TableCell>
            <TableCell>
              <StageSegments stages={project.stages} />
            </TableCell>
            {showClient ? (
              <TableCell>
                <Link
                  href={`/w/${workspaceId}/clients/${project.client.id}`}
                  title={project.client.displayName}
                  aria-label={`Client: ${project.client.displayName}`}
                  className="projects-table-client relative z-10 inline-flex rounded-full"
                >
                  <ClientAvatar name={project.client.displayName} className="size-7" />
                </Link>
              </TableCell>
            ) : null}
            <TableCell className="text-right text-sm text-muted-foreground">{project.dueDate ? formatDateInYear(project.dueDate) : "—"}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
