import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ClientProjectView } from "@/components/projects/ClientProjectView";
import { FirmProjectView } from "@/components/projects/FirmProjectView";
import { requireUser } from "@/lib/auth/session";
import { getClientProject, getProject } from "@/lib/projects/server";
import { getOpenWorkspace } from "@/lib/workspaces/server";

export async function generateMetadata({ params }: PageProps<"/w/[workspaceId]/projects/[projectId]">): Promise<Metadata> {
  const { workspaceId, projectId } = await params;
  const workspace = await getOpenWorkspace(workspaceId);
  if (!workspace) {
    return { title: "Not found" };
  }
  const project = workspace.role === "Client" ? await getClientProject(workspace.id, projectId) : await getProject(workspace.id, projectId);

  return { title: project ? project.name : "Not found" };
}

export default async function ProjectPage({ params }: PageProps<"/w/[workspaceId]/projects/[projectId]">) {
  await requireUser();
  const { workspaceId, projectId } = await params;
  const workspace = await getOpenWorkspace(workspaceId);
  if (!workspace) {
    notFound();
  }

  return workspace.role === "Client" ? (
    <ClientProjectView workspace={workspace} projectId={projectId} />
  ) : (
    <FirmProjectView workspace={workspace} projectId={projectId} />
  );
}
