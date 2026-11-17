import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ClientMeetingsView } from "@/components/meetings/ClientMeetingsView";
import { FirmMeetingsView } from "@/components/meetings/FirmMeetingsView";
import { requireUser } from "@/lib/auth/session";
import { getOpenWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Meetings" };

export default async function MeetingsPage({ params }: PageProps<"/w/[workspaceId]/meetings">) {
  await requireUser();
  const workspace = await getOpenWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }

  return workspace.role === "Client" ? <ClientMeetingsView workspace={workspace} /> : <FirmMeetingsView workspace={workspace} />;
}
