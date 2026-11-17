import { CalendarView } from "@/components/calendar/CalendarView";
import { Page } from "@/components/shell/Page";
import { buildEvents, monthOf, parseMonth, todayUtc } from "@/lib/calendar/calendar";
import { getMeetings } from "@/lib/meetings/server";
import { getProjects } from "@/lib/projects/server";
import type { Workspace } from "@/lib/workspaces/types";

/** The Calendar page as the firm sees it: every project's dates and every meeting, with the client named. */
export async function FirmCalendarView({ workspace, month }: { workspace: Workspace; month: Parameters<typeof parseMonth>[0] }) {
  const [projects, meetings] = await Promise.all([getProjects(workspace.id), getMeetings(workspace.id)]);
  const today = todayUtc();
  const events = buildEvents(
    projects.map((project) => ({ ...project, clientName: project.client.displayName })),
    today,
    meetings.map((meeting) => ({ ...meeting, clientName: meeting.client.displayName })),
  );

  return (
    <Page crumbs={[{ label: "Calendar" }]}>
      <h1 className="font-heading text-3xl font-semibold">Calendar</h1>
      <p className="mb-8 mt-1 text-muted-foreground">Project deadlines, the stages still open and your meetings, by day.</p>
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
