import { FirmCalendar } from "@/components/calendar/FirmCalendar";
import { GoogleResultMessage } from "@/components/account/GoogleResultMessage";
import { GoogleCalendarNotice } from "@/components/meetings/GoogleCalendarNotice";
import { NewMeetingDialog } from "@/components/meetings/NewMeetingDialog";
import { Page } from "@/components/shell/Page";
import { buildEvents, monthOf, parseMonth, todayUtc } from "@/lib/calendar/calendar";
import { getClients } from "@/lib/clients/server";
import { displayName } from "@/lib/clients/types";
import { getGoogleConnection } from "@/lib/google/server";
import { parseGoogleResult } from "@/lib/google/types";
import { getMeetings } from "@/lib/meetings/server";
import { nowIso } from "@/lib/meetings/time";
import { getProjects } from "@/lib/projects/server";
import type { Workspace } from "@/lib/workspaces/types";

/** The Calendar page as the firm sees it: every meeting, with scheduling, editing and cancelling, and the project deadlines on request. */
export async function FirmCalendarView({
  workspace,
  month,
  google: googleParam,
}: {
  workspace: Workspace;
  month: Parameters<typeof parseMonth>[0];
  /** `?google=`: how the way back from Google ended. */
  google?: Parameters<typeof parseGoogleResult>[0];
}) {
  const [projects, meetings, clients, google] = await Promise.all([
    getProjects(workspace.id),
    getMeetings(workspace.id),
    getClients(workspace.id),
    getGoogleConnection(),
  ]);
  const today = todayUtc();
  const events = buildEvents(
    projects.map((project) => ({ ...project, clientName: project.client.displayName })),
    today,
    meetings.map((meeting) => ({ ...meeting, clientName: meeting.client.displayName })),
  );
  const people = clients.map((client) => ({ id: client.id, displayName: displayName(client) }));
  const options = projects.map((project) => ({ id: project.id, name: project.name, clientId: project.client.id }));
  const googleResult = parseGoogleResult(googleParam);

  return (
    <Page crumbs={[{ label: "Calendar" }]}>
      <div className="calendar-header mb-6 flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-heading text-3xl font-semibold">Calendar</h1>
          <p className="mt-1 text-muted-foreground">Calls with your clients, in your Google Calendar.</p>
        </div>
        <NewMeetingDialog workspaceId={workspace.id} clients={people} projects={options} google={google} />
      </div>

      {googleResult ? <GoogleResultMessage result={googleResult} /> : null}
      <GoogleCalendarNotice google={google} returnTo={`/w/${workspace.id}/calendar`} />

      <FirmCalendar
        workspaceId={workspace.id}
        events={events}
        meetings={meetings}
        clients={people}
        projects={options}
        month={parseMonth(month, monthOf(today))}
        today={today}
        now={nowIso()}
      />
    </Page>
  );
}
