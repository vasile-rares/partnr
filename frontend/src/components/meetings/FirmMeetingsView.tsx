import { GoogleCalendarNotice } from "@/components/meetings/GoogleCalendarNotice";
import { FirmMeetingList } from "@/components/meetings/FirmMeetingList";
import { NewMeetingDialog } from "@/components/meetings/NewMeetingDialog";
import { ProjectTabs } from "@/components/projects/ProjectTabs";
import { Page } from "@/components/shell/Page";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { getGoogleConnection } from "@/lib/google/server";
import { getMeetings } from "@/lib/meetings/server";
import { nowIso, splitMeetings } from "@/lib/meetings/time";
import { getProjects } from "@/lib/projects/server";
import type { Workspace } from "@/lib/workspaces/types";

/** The Meetings page as the firm sees it: every meeting, with scheduling, editing and cancelling. */
export async function FirmMeetingsView({ workspace }: { workspace: Workspace }) {
  const [meetings, clients, projects, google] = await Promise.all([
    getMeetings(workspace.id),
    getClients(workspace.id),
    getProjects(workspace.id),
    getGoogleConnection(),
  ]);
  const now = nowIso();
  const { upcoming, past } = splitMeetings(meetings, now);
  const people = clients.map((client) => ({ id: client.id, displayName: displayName(client) }));
  const options = projects.map((project) => ({ id: project.id, name: project.name, clientId: project.client.id }));
  const list = (items: typeof meetings, empty: string) => (
    <FirmMeetingList
      workspaceId={workspace.id}
      meetings={items}
      clients={people}
      projects={options}
      now={now}
      empty={empty}
    />
  );

  return (
    <Page crumbs={[{ label: "Meetings" }]}>
      <div className="meetings-header mb-8 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-semibold">Meetings</h1>
          <p className="mt-1 text-muted-foreground">Calls with your clients, in your Google Calendar.</p>
        </div>
        <NewMeetingDialog workspaceId={workspace.id} clients={people} projects={options} google={google} />
      </div>

      <GoogleCalendarNotice google={google} />

      <ProjectTabs
        tabs={[
          { value: "upcoming", label: "Upcoming", count: upcoming.length },
          { value: "past", label: "Past" },
        ]}
        panels={{
          upcoming: list(upcoming, "No meetings coming up. Schedule one with a client and Google sends them the invitation."),
          past: list(past, "Meetings that took place, or were cancelled, show up here."),
        }}
      />
    </Page>
  );
}
