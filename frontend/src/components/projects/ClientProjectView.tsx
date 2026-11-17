import { notFound } from "next/navigation";
import { ClientDeliverableList } from "@/components/deliverables/ClientDeliverableList";
import { ClientFileList } from "@/components/files/ClientFileList";
import { ClientProjectOverview } from "@/components/projects/ClientProjectOverview";
import { ProjectStatusBadge } from "@/components/projects/ProjectStatusBadge";
import { ProjectTabs, type ProjectTabItem } from "@/components/projects/ProjectTabs";
import { ProjectTimeline } from "@/components/projects/ProjectTimeline";
import { Page } from "@/components/shell/Page";
import { ClientUpdateList } from "@/components/updates/ClientUpdateList";
import { getClientProject } from "@/lib/projects/server";
import type { ClientWorkspace } from "@/lib/workspaces/types";

/** One project as a client sees it: where it stands, what was shared with them and what waits for their answer. */
export async function ClientProjectView({ workspace, projectId }: { workspace: ClientWorkspace; projectId: string }) {
  // The route names the workspace, and the API only answers for a project of that workspace: under another address it is not found.
  const project = await getClientProject(workspace.id, projectId);
  if (!project) {
    notFound();
  }

  // A tab with nothing in it is not offered; Updates stays because its empty state says what to expect.
  const tabs: ProjectTabItem[] = [{ value: "overview", label: "Overview" }];
  if (project.deliverables.length > 0) {
    tabs.push({ value: "deliverables", label: "Deliverables", count: project.awaitingApproval, highlight: true });
  }
  tabs.push({ value: "updates", label: "Updates", count: project.updates.length });
  if (project.files.length > 0) {
    tabs.push({ value: "files", label: "Files", count: project.files.length });
  }

  return (
    <Page crumbs={[{ label: "Projects", href: `/w/${workspace.id}/projects` }, { label: project.name }]} narrow>
      <div className="client-project-header mb-2 flex flex-wrap items-center gap-3">
        <h1 className="font-heading text-3xl font-semibold">{project.name}</h1>
        <ProjectStatusBadge status={project.status} />
      </div>
      <p className="mb-6 text-muted-foreground">With {project.workspace.name}</p>

      <ProjectTimeline project={project} />

      <ProjectTabs
        key={project.id}
        tabs={tabs}
        panels={{
          overview: <ClientProjectOverview project={project} />,
          deliverables: (
            <ClientDeliverableList
              workspaceId={workspace.id}
              workspaceName={project.workspace.name}
              projectId={project.id}
              deliverables={project.deliverables}
            />
          ),
          updates: <ClientUpdateList updates={project.updates} workspaceName={project.workspace.name} />,
          files: <ClientFileList workspaceId={workspace.id} projectId={project.id} files={project.files} />,
        }}
      />
    </Page>
  );
}
