import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ClientHomeView } from "@/components/dashboard/ClientHomeView";
import { FirmHomeView } from "@/components/dashboard/FirmHomeView";
import { requireUser } from "@/lib/auth/session";
import { getOpenWorkspace } from "@/lib/workspaces/server";

export async function generateMetadata({ params }: PageProps<"/w/[workspaceId]">): Promise<Metadata> {
  const workspace = await getOpenWorkspace((await params).workspaceId);
  return { title: workspace?.name ?? "Not found" };
}

export default async function WorkspacePage({ params }: PageProps<"/w/[workspaceId]">) {
  const user = await requireUser();
  // Not a member, not a client and does not exist look the same, on purpose (the API answers 404 for all).
  const workspace = await getOpenWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }

  return workspace.role === "Client" ? (
    <ClientHomeView workspace={workspace} firstName={user.fullName.trim().split(/\s+/)[0]} />
  ) : (
    <FirmHomeView workspace={workspace} />
  );
}
