import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { PortalProjectList } from "@/components/portal/PortalProjectList";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getPortalProjects, getPortalWorkspace } from "@/lib/portal/server";

export const metadata: Metadata = { title: "Projects" };

export default async function PortalProjectsPage({ params }: PageProps<"/portal/[workspaceId]/projects">) {
  await requireUser();
  const workspace = await getPortalWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const projects = (await getPortalProjects()).filter((project) => project.workspace.id === workspace.id);

  return (
    <Page crumbs={[{ label: "Home", href: `/portal/${workspace.id}` }, { label: "Projects" }]} narrow>
      <h1 className="mb-8 font-heading text-3xl font-semibold">Projects</h1>
      <PortalProjectList workspaceId={workspace.id} projects={projects} />
    </Page>
  );
}
