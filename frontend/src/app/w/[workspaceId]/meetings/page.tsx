import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { GoogleCalendarNotice } from "@/components/meetings/GoogleCalendarNotice";
import { MeetingList } from "@/components/meetings/MeetingList";
import { NewMeetingDialog } from "@/components/meetings/NewMeetingDialog";
import { ProjectTabs } from "@/components/projects/ProjectTabs";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { getGoogleConnection } from "@/lib/google/server";
import { getMeetings } from "@/lib/meetings/server";
import { nowIso, splitMeetings } from "@/lib/meetings/time";
import { getProjects } from "@/lib/projects/server";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Meetings" };

export default async function MeetingsPage({ params }: PageProps<"/w/[workspaceId]/meetings">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
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
    <MeetingList
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
