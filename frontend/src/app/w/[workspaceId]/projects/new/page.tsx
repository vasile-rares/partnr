import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ProjectForm } from "@/components/projects/ProjectForm";
import { Page } from "@/components/shell/Page";
import { buttonVariants } from "@/components/ui/button";
import { requireUser } from "@/lib/auth/session";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "New project" };

export default async function NewProjectPage({ params, searchParams }: PageProps<"/w/[workspaceId]/projects/new">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  // Only active clients: an archived one cannot get a new project.
  const clients = (await getClients(workspace.id)).map((client) => ({ id: client.id, displayName: displayName(client) }));
  // Coming from a client's page preselects that client; an id that is not in the list simply selects nothing.
  const requested = (await searchParams).clientId;
  const clientId = typeof requested === "string" && clients.some((c) => c.id === requested) ? requested : undefined;

  return (
    <Page crumbs={[{ label: "Projects", href: `/w/${workspace.id}/projects` }, { label: "New" }]} narrow>
      <h1 className="mb-3 font-heading text-3xl font-semibold">New project</h1>
      <p className="mb-8 max-w-prose text-muted-foreground">
        A new project starts in Planning, so you can prepare it before your client sees it in their portal.
      </p>

      {clients.length === 0 ? (
        <div className="flex max-w-prose flex-col items-start gap-4">
          <p className="text-muted-foreground">A project belongs to a client. Add your first client to start one.</p>
          <Link href={`/w/${workspace.id}/clients/new`} className={buttonVariants()}>
            Add client
          </Link>
        </div>
      ) : (
        <ProjectForm workspaceId={workspace.id} clients={clients} clientId={clientId} />
      )}
    </Page>
  );
}
