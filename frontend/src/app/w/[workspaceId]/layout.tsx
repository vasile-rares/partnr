import { notFound } from "next/navigation";
import { AppShell } from "@/components/shell/AppShell";
import { getClientProjectSummaries, getProjects } from "@/lib/projects/server";
import { getOpenWorkspace } from "@/lib/workspaces/server";

const SIDEBAR_PROJECTS = 5;

export default async function WorkspaceLayout({ children, params }: LayoutProps<"/w/[workspaceId]">) {
  const workspace = await getOpenWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }

  // A client who opens a workspace page gets their own menu, with the projects shared with them.
  if (workspace.role === "Client") {
    const shared = await getClientProjectSummaries(workspace.id);

    return (
      <AppShell
        context={{
          kind: "client",
          workspace: { id: workspace.id, name: workspace.name },
          projects: shared.slice(0, SIDEBAR_PROJECTS).map(({ id, name }) => ({ id, name })),
        }}
      >
        {children}
      </AppShell>
    );
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
