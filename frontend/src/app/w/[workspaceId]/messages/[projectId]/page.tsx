import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { MessagesView } from "@/components/messages/MessagesView";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getConversation, getConversations } from "@/lib/messages/server";
import { firmConversationsApi } from "@/lib/messages/types";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Messages" };

export default async function ConversationPage({ params }: PageProps<"/w/[workspaceId]/messages/[projectId]">) {
  await requireUser();
  const { workspaceId, projectId } = await params;
  const workspace = await getWorkspace(workspaceId);
  if (!workspace) {
    notFound();
  }
  const apiBase = firmConversationsApi(workspace.id);
  const [conversations, open] = await Promise.all([getConversations(apiBase), getConversation(apiBase, projectId)]);
  if (!open) {
    notFound();
  }
  const hrefBase = `/w/${workspace.id}/messages`;

  return (
    <Page crumbs={[{ label: "Home", href: `/w/${workspace.id}` }, { label: "Messages", href: hrefBase }, { label: open.projectName }]}>
      <h1 className="sr-only">Messages about {open.projectName}</h1>
      <MessagesView apiBase={apiBase} hrefBase={hrefBase} conversations={conversations} open={open} />
    </Page>
  );
}
