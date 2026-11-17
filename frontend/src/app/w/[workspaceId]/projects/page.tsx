import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ClientProjectsView } from "@/components/projects/ClientProjectsView";
import { FirmProjectsView } from "@/components/projects/FirmProjectsView";
import { requireUser } from "@/lib/auth/session";
import { getOpenWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Projects" };

export default async function ProjectsPage({ params, searchParams }: PageProps<"/w/[workspaceId]/projects">) {
  await requireUser();
  const workspace = await getOpenWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }

  return workspace.role === "Client" ? (
    <ClientProjectsView workspace={workspace} />
  ) : (
    <FirmProjectsView workspace={workspace} showArchived={(await searchParams).archived === "1"} />
  );
}
