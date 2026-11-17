import { PortalProjectList } from "@/components/portal/PortalProjectList";
import { Page } from "@/components/shell/Page";
import { getPortalProjects } from "@/lib/portal/server";
import type { ClientWorkspace } from "@/lib/workspaces/types";

/** The Projects page as a client sees it: the projects this firm shared with them. */
export async function ClientProjectsView({ workspace }: { workspace: ClientWorkspace }) {
  const projects = await getPortalProjects(workspace.id);

  return (
    <Page crumbs={[{ label: "Home", href: `/w/${workspace.id}` }, { label: "Projects" }]} narrow>
      <h1 className="mb-8 font-heading text-3xl font-semibold">Projects</h1>
      <PortalProjectList workspaceId={workspace.id} projects={projects} />
    </Page>
  );
}
