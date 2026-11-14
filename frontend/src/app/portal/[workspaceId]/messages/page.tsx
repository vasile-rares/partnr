import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { MessagesView } from "@/components/messages/MessagesView";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getConversations } from "@/lib/messages/server";
import { portalConversationsApi } from "@/lib/messages/types";
import { getPortalWorkspace } from "@/lib/portal/server";

export const metadata: Metadata = { title: "Messages" };

export default async function PortalMessagesPage({ params }: PageProps<"/portal/[workspaceId]/messages">) {
  await requireUser();
  const workspace = await getPortalWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const apiBase = portalConversationsApi(workspace.id);

  return (
    <Page crumbs={[{ label: "Home", href: `/portal/${workspace.id}` }, { label: "Messages" }]}>
      <h1 className="sr-only">Messages</h1>
      <MessagesView apiBase={apiBase} hrefBase={`/portal/${workspace.id}/messages`} conversations={await getConversations(apiBase)} />
    </Page>
  );
}
