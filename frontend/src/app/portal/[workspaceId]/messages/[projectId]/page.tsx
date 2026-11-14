import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { MessagesView } from "@/components/messages/MessagesView";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getConversation, getConversations } from "@/lib/messages/server";
import { portalConversationsApi } from "@/lib/messages/types";
import { getPortalWorkspace } from "@/lib/portal/server";

export const metadata: Metadata = { title: "Messages" };

export default async function PortalConversationPage({ params }: PageProps<"/portal/[workspaceId]/messages/[projectId]">) {
  await requireUser();
  const { workspaceId, projectId } = await params;
  const workspace = await getPortalWorkspace(workspaceId);
  if (!workspace) {
    notFound();
  }
  const apiBase = portalConversationsApi(workspace.id);
  const [conversations, open] = await Promise.all([getConversations(apiBase), getConversation(apiBase, projectId)]);
  if (!open) {
    notFound();
  }
  const hrefBase = `/portal/${workspace.id}/messages`;

  return (
    <Page crumbs={[{ label: "Home", href: `/portal/${workspace.id}` }, { label: "Messages", href: hrefBase }, { label: open.projectName }]}>
      <h1 className="sr-only">Messages about {open.projectName}</h1>
      <MessagesView apiBase={apiBase} hrefBase={hrefBase} conversations={conversations} open={open} />
    </Page>
  );
}
