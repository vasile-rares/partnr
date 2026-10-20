import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PortalProjectList } from "@/components/portal/PortalProjectList";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getPortalProjects, getPortalWorkspace } from "@/lib/portal/server";

export async function generateMetadata({ params }: PageProps<"/portal/[workspaceId]">): Promise<Metadata> {
  const workspace = await getPortalWorkspace((await params).workspaceId);
  return { title: workspace?.name ?? "Not found" };
}

export default async function PortalHomePage({ params }: PageProps<"/portal/[workspaceId]">) {
  await requireUser();
  const workspace = await getPortalWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const projects = (await getPortalProjects()).filter((project) => project.workspace.id === workspace.id);

  return (
    <Page crumbs={[{ label: "Home" }]} narrow>
      <h1 className="font-heading text-3xl font-semibold">{workspace.name}</h1>
      <p className="mt-3 mb-8 max-w-prose text-muted-foreground">
        Open a project to see where it stands and what comes next.
      </p>
      <PortalProjectList workspaceId={workspace.id} projects={projects} />
    </Page>
  );
}
