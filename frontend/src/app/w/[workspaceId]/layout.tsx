import { notFound } from "next/navigation";
import { AppShell } from "@/components/shell/AppShell";
import { getProjects } from "@/lib/projects/server";
import { getWorkspace } from "@/lib/workspaces/server";

const SIDEBAR_PROJECTS = 5;

export default async function WorkspaceLayout({ children, params }: LayoutProps<"/w/[workspaceId]">) {
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const active = (await getProjects(workspace.id)).filter((project) => project.status === "Active");

  return (
    <AppShell
      context={{
        kind: "workspace",
        workspace: { id: workspace.id, name: workspace.name, role: workspace.role },
        projects: active.slice(0, SIDEBAR_PROJECTS).map(({ id, name }) => ({ id, name })),
      }}
    >
      {children}
    </AppShell>
  );
}
