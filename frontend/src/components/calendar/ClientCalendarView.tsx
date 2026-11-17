import { CalendarView } from "@/components/calendar/CalendarView";
import { Page } from "@/components/shell/Page";
import { buildEvents, monthOf, parseMonth, todayUtc } from "@/lib/calendar/calendar";
import { getPortalMeetings } from "@/lib/meetings/server";
import { getPortalProject, getPortalProjects } from "@/lib/portal/server";
import type { PortalProject } from "@/lib/portal/types";
import type { ClientWorkspace } from "@/lib/workspaces/types";

/** The Calendar page as a client sees it: what this firm planned on their projects, and their meetings. */
export async function ClientCalendarView({ workspace, month }: { workspace: ClientWorkspace; month: Parameters<typeof parseMonth>[0] }) {
  const summaries = await getPortalProjects(workspace.id);
  // The stages are only in a project's own answer; a client has few projects with one firm, so they are loaded side by side.
  const [loaded, meetings] = await Promise.all([
    Promise.all(summaries.map((summary) => getPortalProject(workspace.id, summary.id))),
    getPortalMeetings(workspace.id),
  ]);
  const projects = loaded.filter((project): project is PortalProject => project !== null);
  const today = todayUtc();
  const events = buildEvents(
    projects.map((project) => ({ ...project, clientName: null })),
    today,
    meetings.map((meeting) => ({ ...meeting, clientName: null })),
  );

  return (
    <Page crumbs={[{ label: "Home", href: `/w/${workspace.id}` }, { label: "Calendar" }]}>
      <h1 className="font-heading text-3xl font-semibold">Calendar</h1>
      <p className="mb-8 mt-1 text-muted-foreground">What {workspace.name} has planned with you, and your meetings, by day.</p>
      <CalendarView
        events={events}
        month={parseMonth(month, monthOf(today))}
        today={today}
        basePath={`/w/${workspace.id}/calendar`}
        projectsPath={`/w/${workspace.id}/projects`}
        meetingsPath={`/w/${workspace.id}/meetings`}
      />
    </Page>
  );
}
