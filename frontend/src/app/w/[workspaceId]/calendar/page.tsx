import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { CalendarView } from "@/components/calendar/CalendarView";
import { Page } from "@/components/shell/Page";
import { requireUser } from "@/lib/auth/session";
import { buildEvents, monthOf, parseMonth, todayUtc } from "@/lib/calendar/calendar";
import { getProjects } from "@/lib/projects/server";
import { getWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Calendar" };

export default async function CalendarPage({ params, searchParams }: PageProps<"/w/[workspaceId]/calendar">) {
  await requireUser();
  const workspace = await getWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const projects = await getProjects(workspace.id);
  const today = todayUtc();
  const events = buildEvents(
    projects.map((project) => ({ ...project, clientName: project.client.displayName })),
    today,
  );

  return (
    <Page crumbs={[{ label: "Calendar" }]}>
      <h1 className="font-heading text-3xl font-semibold">Calendar</h1>
      <p className="mb-8 mt-1 text-muted-foreground">Project deadlines and the stages still open, by day.</p>
      <CalendarView
        events={events}
        month={parseMonth((await searchParams).month, monthOf(today))}
        today={today}
        basePath={`/w/${workspace.id}/calendar`}
        projectsPath={`/w/${workspace.id}/projects`}
      />
    </Page>
  );
}
