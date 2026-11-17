import { PortalActiveProjects } from "@/components/portal/PortalActiveProjects";
import { PortalLatestUpdates } from "@/components/portal/PortalLatestUpdates";
import { PortalNextMeeting } from "@/components/portal/PortalNextMeeting";
import { PortalWaitingForYou } from "@/components/portal/PortalWaitingForYou";
import { Page } from "@/components/shell/Page";
import { getPortalMeetings } from "@/lib/meetings/server";
import { nowIso, splitMeetings } from "@/lib/meetings/time";
import { getPortalProject, getPortalProjects } from "@/lib/portal/server";
import type { PortalProject } from "@/lib/portal/types";
import type { ClientWorkspace } from "@/lib/workspaces/types";

const LATEST_UPDATES = 5;

/** The workspace's Home as a client sees it: what waits for them, what runs, the latest news and the next meeting. */
export async function ClientHomeView({ workspace, firstName }: { workspace: ClientWorkspace; firstName: string }) {
  const summaries = await getPortalProjects(workspace.id);
  // A client has few projects with one firm, so the details of each are loaded side by side instead of adding an endpoint.
  const projects = (await Promise.all(summaries.map((summary) => getPortalProject(workspace.id, summary.id)))).filter(
    (project): project is PortalProject => project !== null,
  );

  const waiting = projects.flatMap((project) =>
    project.deliverables
      .filter((deliverable) => deliverable.approval.status === "Pending")
      .map((deliverable) => ({
        id: deliverable.id,
        title: deliverable.title,
        note: deliverable.approval.pending?.note ?? null,
        projectId: project.id,
        projectName: project.name,
      })),
  );
  const running = projects.filter((project) => project.status === "Active" || project.status === "OnHold");
  const updates = projects
    .flatMap((project) => project.updates.map((update) => ({ ...update, projectId: project.id, projectName: project.name })))
    .sort((a, b) => b.sharedAtUtc.localeCompare(a.sharedAtUtc))
    .slice(0, LATEST_UPDATES);
  const nextMeeting = splitMeetings(await getPortalMeetings(workspace.id), nowIso()).upcoming[0] ?? null;

  return (
    <Page crumbs={[{ label: "Home" }]} narrow>
      <h1 className="font-heading text-3xl font-semibold">{workspace.name}</h1>
      <p className="mb-8 mt-1 max-w-prose text-muted-foreground">
        Welcome back, {firstName}. Here&apos;s where things stand with {workspace.name}.
      </p>

      <div className="portal-home flex flex-col gap-10">
        <PortalNextMeeting workspaceId={workspace.id} meeting={nextMeeting} />
        {projects.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Nothing has been shared with you yet. Projects show up here as soon as they are ready.
          </p>
        ) : (
          <>
            <PortalWaitingForYou workspaceId={workspace.id} workspaceName={workspace.name} items={waiting} />
            <PortalActiveProjects workspaceId={workspace.id} projects={running} />
            <PortalLatestUpdates workspaceId={workspace.id} updates={updates} />
          </>
        )}
      </div>
    </Page>
  );
}
