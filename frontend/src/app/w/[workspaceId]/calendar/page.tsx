import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { ClientCalendarView } from "@/components/calendar/ClientCalendarView";
import { FirmCalendarView } from "@/components/calendar/FirmCalendarView";
import { requireUser } from "@/lib/auth/session";
import { getOpenWorkspace } from "@/lib/workspaces/server";

export const metadata: Metadata = { title: "Calendar" };

export default async function CalendarPage({ params, searchParams }: PageProps<"/w/[workspaceId]/calendar">) {
  await requireUser();
  const workspace = await getOpenWorkspace((await params).workspaceId);
  if (!workspace) {
    notFound();
  }
  const { month } = await searchParams;

  return workspace.role === "Client" ? (
    <ClientCalendarView workspace={workspace} month={month} />
  ) : (
    <FirmCalendarView workspace={workspace} month={month} />
  );
}
