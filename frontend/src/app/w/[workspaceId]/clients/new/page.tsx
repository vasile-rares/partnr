import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { AddClientForm } from "@/components/clients/AddClientForm";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getClients } from "@/lib/clients/server";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Add client" };

export default async function NewClientPage({ params }: PageProps<"/w/[workspaceId]/clients/new">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  // Every client, archived ones too: the email is unique in the workspace, so the form can warn about a duplicate early.
  const existingEmails = (await getClients(workspace.id, true)).map((client) => client.email);

  return (
    <Page crumbs={[{ label: "Clients", href: `/w/${workspace.id}/clients` }, { label: "Add" }]} narrow>
      <h1 className="mb-3 font-heading text-3xl font-semibold">Add a client</h1>
      <p className="mb-8 max-w-prose text-muted-foreground">
        Enter their email. They get an invitation to follow their projects, and their name, phone and company come from
        their own account once they accept.
      </p>

      <AddClientForm workspaceId={workspace.id} existingEmails={existingEmails} />
    </Page>
  );
}
