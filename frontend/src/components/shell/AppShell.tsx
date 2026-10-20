import { cookies } from "next/headers";
import type { ReactNode } from "react";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { requireUser } from "@/lib/auth/session";
import { getPortalWorkspaces } from "@/lib/portal/server";
import { getMyWorkspaces } from "@/lib/workspaces/server";
import { AppSidebar } from "./AppSidebar";
import type { ShellContext } from "./types";

// The cookie the sidebar component writes when it is opened or closed; reading it here avoids a flash on the next load.
const SIDEBAR_COOKIE = "sidebar_state";

/** Sidebar and frame around a page. Pages still call `requireUser()` themselves: a layout does not re-render on navigation. */
export async function AppShell({ context, children }: { context: ShellContext; children: ReactNode }) {
  const [user, workspaces, clientOf, cookieStore] = await Promise.all([
    requireUser(),
    getMyWorkspaces(),
    getPortalWorkspaces(),
    cookies(),
  ]);

  return (
    <SidebarProvider defaultOpen={cookieStore.get(SIDEBAR_COOKIE)?.value !== "false"}>
      <AppSidebar
        context={context}
        user={{ fullName: user.fullName, email: user.email }}
        workspaces={workspaces.map(({ id, name }) => ({ id, name }))}
        clientOf={clientOf}
      />
      <SidebarInset>{children}</SidebarInset>
    </SidebarProvider>
  );
}
