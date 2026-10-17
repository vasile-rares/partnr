import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { AppHeader } from "@/components/AppHeader";
import { ArchiveClientButton } from "@/components/clients/ArchiveClientButton";
import { ClientForm } from "@/components/clients/ClientForm";
import { ClientPortalActions } from "@/components/clients/ClientPortalActions";
import { PortalStatusBadge } from "@/components/clients/PortalStatusBadge";
import { ProjectsTable } from "@/components/projects/ProjectsTable";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { buttonVariants } from "@/components/ui/button";
import { requireUser } from "@/lib/auth/session";
import { getClient } from "@/lib/clients/server";
import { displayName, type Client } from "@/lib/clients/types";
import { formatDateTimeUtc } from "@/lib/format";
import { getProjects } from "@/lib/projects/server";
import { getWorkspace } from "@/lib/workspaces/server";

export async function generateMetadata({ params }: PageProps<"/w/[workspaceId]/clients/[clientId]">): Promise<Metadata> {
  const { workspaceId, clientId } = await params;
  const workspace = await getWorkspace(workspaceId);
  const client = workspace ? await getClient(workspace.id, clientId) : null;
  return { title: client ? displayName(client) : "Not found" };
}

export default async function ClientPage({ params, searchParams }: PageProps<"/w/[workspaceId]/clients/[clientId]">) {
  await requireUser();
  const { workspaceId, clientId } = await params;
  const workspace = await getWorkspace(workspaceId);
  if (!workspace) {
    notFound();
  }
  // The add-client form redirects here with this flag when the client was saved but the email could not go out.
  const inviteFailed = (await searchParams).invite === "failed";
  const client = await getClient(workspace.id, clientId);
  if (!client) {
    notFound();
  }
  // Archived projects too, marked as such: this page is the client's whole history.
  const projects = await getProjects(workspace.id, { includeArchived: true, clientId: client.id });

  return (
    <>
      <AppHeader workspace={workspace} />
      <main className="mx-auto w-full max-w-3xl px-6 py-10">
        <Link href={`/w/${workspace.id}/clients`} className="text-sm text-muted-foreground hover:underline">
          ← Clients
        </Link>
        <h1 className="mb-8 mt-2 font-heading text-3xl font-semibold">{displayName(client)}</h1>

        {inviteFailed ? (
          <Alert variant="destructive" className="mb-8 max-w-lg">
            <AlertDescription>
              The client was added, but the invitation could not be sent. Try again with the button below.
            </AlertDescription>
          </Alert>
        ) : null}

        {client.archivedAtUtc ? (
          <Alert className="mb-8 max-w-lg">
            <AlertDescription>
              This client is archived. It is hidden from the clients list until you restore it.
            </AlertDescription>
          </Alert>
        ) : null}

        <section aria-labelledby="portal-title" className="mb-10 max-w-lg">
          <div className="flex items-center gap-3">
            <h2 id="portal-title" className="font-heading text-xl font-semibold">
              Client portal
            </h2>
            <PortalStatusBadge status={client.portal.status} />
          </div>
          <p className="mb-4 mt-2 text-sm text-muted-foreground">{portalDescription(client)}</p>
          <ClientPortalActions workspaceId={workspace.id} client={client} />
        </section>

        <section aria-labelledby="projects-title" className="mb-10">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-4">
            <h2 id="projects-title" className="font-heading text-xl font-semibold">
              Projects
            </h2>
            {client.archivedAtUtc ? null : (
              <Link
                href={`/w/${workspace.id}/projects/new?clientId=${client.id}`}
                className={buttonVariants({ variant: "outline" })}
              >
                New project
              </Link>
            )}
          </div>
          {projects.length === 0 ? (
            <p className="text-sm text-muted-foreground">No projects for this client yet.</p>
          ) : (
            <ProjectsTable workspaceId={workspace.id} projects={projects} showClient={false} />
          )}
        </section>

        <ClientForm workspaceId={workspace.id} client={client} />

        <section aria-labelledby="archive-title" className="mt-12 max-w-lg border-t pt-8">
          <h2 id="archive-title" className="font-heading text-xl font-semibold">
            {client.archivedAtUtc ? "Restore client" : "Archive client"}
          </h2>
          {/* The API enforces this too; hiding the button only spares a member an error they cannot fix. */}
          {workspace.role === "Owner" ? (
            <>
              <p className="mb-4 mt-2 text-sm text-muted-foreground">
                {client.archivedAtUtc
                  ? "Bring this client back to the clients list."
                  : "Hide this client from the list. Nothing is deleted, and you can restore it at any time."}
              </p>
              <ArchiveClientButton workspaceId={workspace.id} client={client} />
            </>
          ) : (
            <p className="mt-2 text-sm text-muted-foreground">
              Only the workspace owner can archive or restore clients.
            </p>
          )}
        </section>
      </main>
    </>
  );
}

function portalDescription(client: Client): string {
  const expires = client.portal.expiresAtUtc ? formatDateTimeUtc(client.portal.expiresAtUtc) : null;

  switch (client.portal.status) {
    case "Active":
      return `${displayName(client)} has an account and can follow their projects in the portal. Their name, email, phone and company come from their own account, so only the notes are yours to edit.`;
    case "Invited":
      return `An invitation was sent to ${client.email}. It expires on ${expires}. Until then the details below are locked, so the invitation keeps matching this address. Cancel the invitation to change them.`;
    case "Expired":
      return `The invitation to ${client.email} expired on ${expires}. Send a new one when you're ready.`;
    case "NotInvited":
      return client.archivedAtUtc
        ? "Restore this client to invite them to the portal."
        : "Invite this client by email so they can follow their projects in the portal. Once they accept, their name, email, phone and company come from their own account and can't be edited here.";
  }
}
