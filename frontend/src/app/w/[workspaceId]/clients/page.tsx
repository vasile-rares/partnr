import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { AddClientDialog } from "@/components/clients/AddClientDialog";
import { ClientsTable, type ClientRow } from "@/components/clients/ClientsTable";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { getProjects } from "@/lib/projects/server";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Clients" };

export default async function ClientsPage({ params, searchParams }: PageProps<"/w/[workspaceId]/clients">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  // Archived clients too: the table switches between them without asking again, and the email is unique in the
  // workspace, so the add form can warn about a duplicate early.
  const [clients, projects] = await Promise.all([getClients(workspace.id, true), getProjects(workspace.id)]);

  const activeProjects = new Map<string, number>();
  for (const project of projects) {
    if (project.status !== "Completed") {
      activeProjects.set(project.client.id, (activeProjects.get(project.client.id) ?? 0) + 1);
    }
  }

  const rows: ClientRow[] = clients.map((client) => ({
    id: client.id,
    displayName: displayName(client),
    email: client.email,
    company: client.company,
    archived: client.archivedAtUtc !== null,
    portalStatus: client.portal.status,
    activeProjects: activeProjects.get(client.id) ?? 0,
  }));
  const addClient = <AddClientDialog workspaceId={workspace.id} existingEmails={clients.map((client) => client.email)} />;

  return (
    <Page crumbs={[{ label: "Clients" }]}>
      <div className="mb-8 flex flex-wrap items-center justify-between gap-4">
        <h1 className="font-heading text-3xl font-semibold">Clients</h1>
        {rows.length > 0 ? addClient : null}
      </div>

      <ClientsTable
        workspaceId={workspace.id}
        clients={rows}
        initialScope={(await searchParams).archived === "1" ? "archived" : "active"}
        addClient={addClient}
      />
    </Page>
  );
}
