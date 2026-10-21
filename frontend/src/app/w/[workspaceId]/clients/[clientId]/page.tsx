import { CalendarIcon, MailIcon, PencilIcon, PhoneIcon } from "lucide-react";
import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { InfoTip } from "@/components/actions/InfoTip";
import { ArchiveClientButton } from "@/components/clients/ArchiveClientButton";
import { ClientAvatar } from "@/components/clients/ClientAvatar";
import { ClientForm } from "@/components/clients/ClientForm";
import { ClientNotesCard } from "@/components/clients/ClientNotesCard";
import { ClientPortalCard } from "@/components/clients/ClientPortalCard";
import { NewProjectDialog } from "@/components/projects/NewProjectDialog";
import { ProjectsTable } from "@/components/projects/ProjectsTable";
import { Page } from "@/components/shell/Page";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { requireUser } from "@/lib/auth/session";
import { getClient } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { formatDate } from "@/lib/format";
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
  const archived = client.archivedAtUtc !== null;
  // Their email is the title until they have a name; then it is a line of its own.
  const showEmail = client.name !== null;

  return (
    <Page crumbs={[{ label: "Clients", href: `/w/${workspace.id}/clients` }, { label: displayName(client) }]}>
      <Sheet>
        <div className="mb-8 flex flex-wrap items-start justify-between gap-4">
          <div className="flex min-w-0 items-center gap-4">
            <ClientAvatar name={displayName(client)} className="size-14 text-lg" />
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="break-all font-heading text-3xl font-semibold">{displayName(client)}</h1>
                <InfoTip label="When this client was added" icon={<CalendarIcon />}>
                  Added {formatDate(client.createdAtUtc.slice(0, 10))}
                </InfoTip>
              </div>
              {client.company ? <p className="mt-1 text-muted-foreground">{client.company}</p> : null}
              <ul className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
                {showEmail ? (
                  <li className="flex items-center gap-1.5">
                    <MailIcon className="size-4" aria-hidden />
                    <a href={`mailto:${client.email}`} className="break-all hover:text-foreground hover:underline">
                      {client.email}
                    </a>
                  </li>
                ) : null}
                {client.phone ? (
                  <li className="flex items-center gap-1.5">
                    <PhoneIcon className="size-4" aria-hidden />
                    <a href={`tel:${client.phone}`} className="hover:text-foreground hover:underline">
                      {client.phone}
                    </a>
                  </li>
                ) : null}
              </ul>
            </div>
          </div>
          <SheetTrigger render={<Button variant="outline" />}>
            <PencilIcon data-icon="inline-start" />
            Edit client
          </SheetTrigger>
        </div>

        {inviteFailed ? (
          <Alert variant="destructive" className="mb-8">
            <AlertDescription>
              The client was added, but the invitation could not be sent. Try again with the button below.
            </AlertDescription>
          </Alert>
        ) : null}

        {archived ? (
          <Alert className="mb-8">
            <AlertDescription>
              This client is archived. It is hidden from the clients list until you restore it.
            </AlertDescription>
          </Alert>
        ) : null}

        <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_20rem]">
          <section aria-labelledby="projects-title">
            <div className="mb-4 flex flex-wrap items-center justify-between gap-4">
              <h2 id="projects-title" className="font-heading text-xl font-semibold">
                Projects
              </h2>
              {archived ? null : (
                <NewProjectDialog
                  workspaceId={workspace.id}
                  clients={[{ id: client.id, displayName: displayName(client) }]}
                  clientId={client.id}
                />
              )}
            </div>
            {projects.length === 0 ? (
              <p className="rounded-lg border border-dashed p-6 text-sm text-muted-foreground">No projects for this client yet.</p>
            ) : (
              <ProjectsTable workspaceId={workspace.id} projects={projects} showClient={false} />
            )}
          </section>
          <aside aria-label="Client summary" className="order-first flex flex-col gap-4 lg:order-0">
            <ClientPortalCard workspaceId={workspace.id} client={client} />
            <ClientNotesCard notes={client.notes} />
          </aside>
        </div>

        <SheetContent className="data-[side=right]:w-full data-[side=right]:sm:max-w-lg">
          <SheetHeader>
            <SheetTitle>Edit client</SheetTitle>
            <SheetDescription>Contact details and your private notes about this client.</SheetDescription>
          </SheetHeader>
          <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-6">
            <ClientForm workspaceId={workspace.id} client={client} />

            <Separator className="my-8" />

            <section aria-labelledby="archive-title">
              <h2 id="archive-title" className="font-heading text-xl font-semibold">
                {archived ? "Restore client" : "Archive client"}
              </h2>
              {/* The API enforces this too; hiding the button only spares a member an error they cannot fix. */}
              {workspace.role === "Owner" ? (
                <>
                  <p className="mb-4 mt-2 text-sm text-muted-foreground">
                    {archived
                      ? "Bring this client back to the clients list."
                      : "Hide this client from the list. Nothing is deleted, and you can restore it at any time."}
                  </p>
                  <ArchiveClientButton workspaceId={workspace.id} client={client} />
                </>
              ) : (
                <p className="mt-2 text-sm text-muted-foreground">Only the workspace owner can archive or restore clients.</p>
              )}
            </section>
          </div>
        </SheetContent>
      </Sheet>
    </Page>
  );
}
