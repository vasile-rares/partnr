import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { CalendarView } from "@/components/calendar/CalendarView";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { buildEvents, monthOf, parseMonth, todayUtc } from "@/lib/calendar/calendar";
import { getPortalProject, getPortalProjects, getPortalWorkspace } from "@/lib/portal/server";
import type { PortalProject } from "@/lib/portal/types";

export const metadata: Metadata = { title: "Calendar" };

export default async function PortalCalendarPage({ params, searchParams }: PageProps<"/portal/[workspaceId]/calendar">) {
  await requireUser();
  const workspace = await getPortalWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const summaries = (await getPortalProjects()).filter((project) => project.workspace.id === workspace.id);
  // The stages are only in a project's own answer; a client has few projects with one firm, so they are loaded side by side.
  const projects = (await Promise.all(summaries.map((summary) => getPortalProject(summary.id)))).filter(
    (project): project is PortalProject => project !== null,
  );
  const today = todayUtc();
  const events = buildEvents(
    projects.map((project) => ({ ...project, clientName: null })),
    today,
  );

  return (
    <Page crumbs={[{ label: "Home", href: `/portal/${workspace.id}` }, { label: "Calendar" }]}>
      <h1 className="font-heading text-3xl font-semibold">Calendar</h1>
      <p className="mb-8 mt-1 text-muted-foreground">What {workspace.name} has planned with you, by day.</p>
      <CalendarView
        events={events}
        month={parseMonth((await searchParams).month, monthOf(today))}
        today={today}
        basePath={`/portal/${workspace.id}/calendar`}
        projectsPath={`/portal/${workspace.id}/projects`}
      />
    </Page>
  );
}
