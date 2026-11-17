import { PortalMeetingList } from "@/components/portal/PortalMeetingList";
import { ProjectTabs } from "@/components/projects/ProjectTabs";
import { Page } from "@/components/shell/Page";
import { getPortalMeetings } from "@/lib/meetings/server";
import { nowIso, splitMeetings } from "@/lib/meetings/time";
import type { ClientWorkspace } from "@/lib/workspaces/types";

/** The Meetings page as a client sees it: their calls with this firm, to join or to look back on. */
export async function ClientMeetingsView({ workspace }: { workspace: ClientWorkspace }) {
  const { upcoming, past } = splitMeetings(await getPortalMeetings(workspace.id), nowIso());

  return (
    <Page crumbs={[{ label: "Home", href: `/w/${workspace.id}` }, { label: "Meetings" }]} narrow>
      <h1 className="font-heading text-3xl font-semibold">Meetings</h1>
      <p className="mb-8 mt-1 text-muted-foreground">Your calls with {workspace.name}. Google also emails you each invitation.</p>

      <ProjectTabs
        tabs={[
          { value: "upcoming", label: "Upcoming", count: upcoming.length },
          { value: "past", label: "Past" },
        ]}
        panels={{
          upcoming: (
            <PortalMeetingList
              workspaceId={workspace.id}
              meetings={upcoming}
              joinable
              empty={`No meetings are planned with ${workspace.name} yet. They show up here, and in your email, once one is scheduled.`}
            />
          ),
          past: <PortalMeetingList workspaceId={workspace.id} meetings={past} empty="Meetings that took place, or were cancelled, show up here." />,
        }}
      />
    </Page>
  );
}
