import type { ReactNode } from "react";
import { AppShell } from "@/components/shell/AppShell";
import { getPortalProjects } from "@/lib/portal/server";

const SIDEBAR_PROJECTS = 5;

export default async function PortalLayout({ children }: { children: ReactNode }) {
  const projects = await getPortalProjects();

  return (
    <AppShell
      context={{ kind: "portal", projects: projects.slice(0, SIDEBAR_PROJECTS).map(({ id, name }) => ({ id, name })) }}
    >
      {children}
    </AppShell>
  );
}
