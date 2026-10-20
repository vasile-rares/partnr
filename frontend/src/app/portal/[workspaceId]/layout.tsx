import { notFound } from "next/navigation";
import { AppShell } from "@/components/shell/AppShell";
import { getPortalProjects, getPortalWorkspace } from "@/lib/portal/server";

const SIDEBAR_PROJECTS = 5;

export default async function PortalWorkspaceLayout({ children, params }: LayoutProps<"/portal/[workspaceId]">) {
  const workspace = await getPortalWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const projects = (await getPortalProjects()).filter((project) => project.workspace.id === workspace.id);

  return (
    <AppShell
      context={{
        kind: "portal",
        workspace: { id: workspace.id, name: workspace.name },
        projects: projects.slice(0, SIDEBAR_PROJECTS).map(({ id, name }) => ({ id, name })),
      }}
    >
      {children}
    </AppShell>
  );
}
