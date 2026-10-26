import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ActiveProjects } from "@/components/dashboard/ActiveProjects";
import { ComingUp } from "@/components/dashboard/ComingUp";
import { GettingStarted } from "@/components/dashboard/GettingStarted";
import { NeedsAttention } from "@/components/dashboard/NeedsAttention";
import { WaitingOnClients } from "@/components/dashboard/WaitingOnClients";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { needingAttention, openProjects, upcomingDeadlines } from "@/lib/dashboard/deadlines";
import { getProjects } from "@/lib/projects/server";
import { getWorkspace } from "@/lib/workspaces/server";

export async function generateMetadata({ params }: PageProps<"/w/[workspaceId]">): Promise<Metadata> {
  const workspace = await getWorkspace((await params).workspaceId);
  return { title: workspace?.name ?? "Not found" };
}

export default async function WorkspacePage({ params }: PageProps<"/w/[workspaceId]">) {
  await requireUser();
  // Not a member and does not exist look the same, on purpose (the API answers 404 for both).
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const [clients, projects] = await Promise.all([getClients(workspace.id), getProjects(workspace.id)]);

  const waiting = clients
    .filter((client) => client.portal.status === "Invited" || client.portal.status === "Expired")
    .map((client) => ({ id: client.id, displayName: displayName(client), portalStatus: client.portal.status }));
  const running = openProjects(projects);

  return (
    <Page crumbs={[{ label: "Home" }]}>
      <h1 className="font-heading text-3xl font-semibold">{workspace.name}</h1>
      <p className="mb-8 mt-1 text-muted-foreground">What is due, what is running, and who you are waiting on.</p>

      <div className="grid items-start gap-10 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="flex flex-col gap-10">
          {projects.length === 0 ? (
            <GettingStarted
              workspaceId={workspace.id}
              clients={clients.map((client) => ({ id: client.id, displayName: displayName(client) }))}
              existingEmails={clients.map((client) => client.email)}
            />
          ) : (
            <>
              <NeedsAttention workspaceId={workspace.id} projects={needingAttention(projects)} />
              <ComingUp workspaceId={workspace.id} deadlines={upcomingDeadlines(projects)} />
              <ActiveProjects workspaceId={workspace.id} projects={running} />
            </>
          )}
        </div>
        {clients.length > 0 ? <WaitingOnClients workspaceId={workspace.id} clients={waiting} /> : null}
      </div>
    </Page>
  );
}
