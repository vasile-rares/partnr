import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PortalActiveProjects } from "@/components/portal/PortalActiveProjects";
import { PortalLatestUpdates } from "@/components/portal/PortalLatestUpdates";
import { PortalWaitingForYou } from "@/components/portal/PortalWaitingForYou";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getPortalProject, getPortalProjects, getPortalWorkspace } from "@/lib/portal/server";
import type { PortalProject } from "@/lib/portal/types";

const LATEST_UPDATES = 5;

export async function generateMetadata({ params }: PageProps<"/portal/[workspaceId]">): Promise<Metadata> {
  const workspace = await getPortalWorkspace((await params).workspaceId);
  return { title: workspace?.name ?? "Not found" };
}

export default async function PortalHomePage({ params }: PageProps<"/portal/[workspaceId]">) {
  const user = await requireUser();
  const workspace = await getPortalWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const summaries = (await getPortalProjects()).filter((project) => project.workspace.id === workspace.id);
  // A client has few projects with one firm, so the details of each are loaded side by side instead of adding an endpoint.
  const projects = (await Promise.all(summaries.map((summary) => getPortalProject(summary.id)))).filter(
    (project): project is PortalProject => project !== null,
  );

  const waiting = projects.flatMap((project) =>
    project.deliverables
      .filter((deliverable) => deliverable.approval.status === "Pending")
      .map((deliverable) => ({
        id: deliverable.id,
        title: deliverable.title,
        note: deliverable.approval.pending?.note ?? null,
        projectId: project.id,
        projectName: project.name,
      })),
  );
  const running = projects.filter((project) => project.status === "Active" || project.status === "OnHold");
  const updates = projects
    .flatMap((project) => project.updates.map((update) => ({ ...update, projectId: project.id, projectName: project.name })))
    .sort((a, b) => b.sharedAtUtc.localeCompare(a.sharedAtUtc))
    .slice(0, LATEST_UPDATES);
  const firstName = user.fullName.trim().split(/\s+/)[0];

  return (
    <Page crumbs={[{ label: "Home" }]} narrow>
      <h1 className="font-heading text-3xl font-semibold">{workspace.name}</h1>
      <p className="mb-8 mt-1 max-w-prose text-muted-foreground">
        Welcome back, {firstName}. Here&apos;s where things stand with {workspace.name}.
      </p>

      {projects.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Nothing has been shared with you yet. Projects show up here as soon as they are ready.
        </p>
      ) : (
        <div className="flex flex-col gap-10">
          <PortalWaitingForYou workspaceId={workspace.id} workspaceName={workspace.name} items={waiting} />
          <PortalActiveProjects workspaceId={workspace.id} projects={running} />
          <PortalLatestUpdates workspaceId={workspace.id} updates={updates} />
        </div>
      )}
    </Page>
  );
}
