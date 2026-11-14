import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { MessagesView } from "@/components/messages/MessagesView";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getConversations } from "@/lib/messages/server";
import { firmConversationsApi } from "@/lib/messages/types";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Messages" };

export default async function MessagesPage({ params }: PageProps<"/w/[workspaceId]/messages">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const apiBase = firmConversationsApi(workspace.id);

  return (
    <Page crumbs={[{ label: "Home", href: `/w/${workspace.id}` }, { label: "Messages" }]}>
      <h1 className="sr-only">Messages</h1>
      <MessagesView apiBase={apiBase} hrefBase={`/w/${workspace.id}/messages`} conversations={await getConversations(apiBase)} />
    </Page>
  );
}
