import { FirmActiveProjects } from "@/components/dashboard/FirmActiveProjects";
import { ComingUp } from "@/components/dashboard/ComingUp";
import { GettingStarted } from "@/components/dashboard/GettingStarted";
import { NeedsAttention } from "@/components/dashboard/NeedsAttention";
import { WaitingOnClients } from "@/components/dashboard/WaitingOnClients";
import { Page } from "@/components/shell/Page";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { needingAttention, openProjects, upcomingDeadlines } from "@/lib/dashboard/deadlines";
import { getProjects } from "@/lib/projects/server";
import type { Workspace } from "@/lib/workspaces/types";

/** The workspace's Home as the firm sees it: what needs attention, what is due, what runs and who it is waiting on. */
export async function FirmHomeView({ workspace }: { workspace: Workspace }) {
  const [clients, projects] = await Promise.all([getClients(workspace.id), getProjects(workspace.id)]);

  const waiting = clients
    .filter((client) => client.portal.status === "Invited" || client.portal.status === "Expired")
    .map((client) => ({ id: client.id, displayName: displayName(client), portalStatus: client.portal.status }));
  const running = openProjects(projects);

  return (
    <Page crumbs={[{ label: "Home" }]}>
      <h1 className="font-heading text-3xl font-semibold">{workspace.name}</h1>
      <p className="mb-8 mt-1 text-muted-foreground">What is due, what is running, and who you are waiting on.</p>

      <div className="dashboard grid items-start gap-10 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="dashboard-main flex flex-col gap-10">
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
              <FirmActiveProjects workspaceId={workspace.id} projects={running} />
            </>
          )}
        </div>
        {clients.length > 0 ? <WaitingOnClients workspaceId={workspace.id} clients={waiting} /> : null}
      </div>
    </Page>
  );
}
