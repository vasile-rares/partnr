import Link from "next/link";
import { NewProjectDialog } from "@/components/projects/NewProjectDialog";
import { ProjectsTable } from "@/components/projects/ProjectsTable";
import { Page } from "@/components/shell/Page";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { getProjects } from "@/lib/projects/server";
import type { Workspace } from "@/lib/workspaces/types";

/** The Projects page as the firm sees it: every project, archived ones on request, and a way to start one. */
export async function FirmProjectsView({ workspace, showArchived }: { workspace: Workspace; showArchived: boolean }) {
  const projects = await getProjects(workspace.id, { includeArchived: showArchived });
  const base = `/w/${workspace.id}/projects`;
  // Active clients only: an archived one cannot get a new project.
  const clients = (await getClients(workspace.id)).map((client) => ({ id: client.id, displayName: displayName(client) }));

  return (
    <Page crumbs={[{ label: "Projects" }]}>
      <div className="projects-header mb-8 flex flex-wrap items-center justify-between gap-4">
        <h1 className="font-heading text-3xl font-semibold">Projects</h1>
        <NewProjectDialog workspaceId={workspace.id} clients={clients} />
      </div>

      <p className="mb-4 text-sm">
        <Link href={showArchived ? base : `${base}?archived=1`} className="text-muted-foreground hover:underline">
          {showArchived ? "Hide archived projects" : "Show archived projects"}
        </Link>
      </p>

      {projects.length === 0 ? (
        <p className="max-w-prose text-muted-foreground">
          {showArchived
            ? "No projects yet. Start one for a client, and they can follow it in their portal."
            : "No active projects. Start one for a client, or show the archived ones."}
        </p>
      ) : (
        <ProjectsTable workspaceId={workspace.id} projects={projects} />
      )}
    </Page>
  );
}
