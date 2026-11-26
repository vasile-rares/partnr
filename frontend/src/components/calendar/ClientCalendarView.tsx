import { ClientCalendar } from "@/components/calendar/ClientCalendar";
import { Page } from "@/components/shell/Page";
import { buildEvents, monthOf, parseMonth, todayUtc } from "@/lib/calendar/calendar";
import { getClientMeetings } from "@/lib/meetings/server";
import { nowIso } from "@/lib/meetings/time";
import { getClientProjects } from "@/lib/projects/server";
import type { ClientWorkspace } from "@/lib/workspaces/types";

/** The Calendar page as a client sees it: their calls with this firm, to join or to look back on, and what the firm planned on their projects on request. */
export async function ClientCalendarView({ workspace, month }: { workspace: ClientWorkspace; month: Parameters<typeof parseMonth>[0] }) {
  const [projects, meetings] = await Promise.all([getClientProjects(workspace.id), getClientMeetings(workspace.id)]);
  const today = todayUtc();
  const events = buildEvents(
    projects.map((project) => ({ ...project, clientName: null })),
    today,
    meetings.map((meeting) => ({ ...meeting, clientName: null })),
  );

  return (
    <Page crumbs={[{ label: "Home", href: `/w/${workspace.id}` }, { label: "Calendar" }]}>
      <h1 className="font-heading text-3xl font-semibold">Calendar</h1>
      <p className="mb-6 mt-1 text-muted-foreground">Your calls with {workspace.name}. Google also emails you each invitation.</p>
      <ClientCalendar
        workspaceId={workspace.id}
        events={events}
        meetings={meetings}
        month={parseMonth(month, monthOf(today))}
        today={today}
        now={nowIso()}
      />
    </Page>
  );
}
