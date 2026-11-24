import { cookies } from "next/headers";
import type { ReactNode } from "react";
import { NotificationsProvider } from "@/components/notifications/NotificationsProvider";
import { SidebarInset, SidebarProvider } from "@/components/ui/sidebar";
import { requireUser } from "@/lib/auth/session";
import { getClientWorkspaces, getMyWorkspaces } from "@/lib/workspaces/server";
import { AppSidebar } from "./AppSidebar";
import type { ShellContext } from "./types";

// The cookie the sidebar component writes when it is opened or closed; reading it here avoids a flash on the next load.
const SIDEBAR_COOKIE = "sidebar_state";

/** Sidebar and frame around a page. Pages still call `requireUser()` themselves: a layout does not re-render on navigation. */
export async function AppShell({ context, children }: { context: ShellContext; children: ReactNode }) {
  const [user, workspaces, clientOf, cookieStore] = await Promise.all([
    requireUser(),
    getMyWorkspaces(),
    getClientWorkspaces(),
    cookies(),
  ]);

  // The notifications provider is here, not in `Page`: a layout stays mounted while the pages change, so the hub connection survives navigation.
  return (
    <NotificationsProvider>
      <a
        href="#main-content"
        className="skip-link sr-only focus-visible:not-sr-only focus-visible:fixed focus-visible:top-2 focus-visible:left-2 focus-visible:z-50 focus-visible:rounded-md focus-visible:bg-background focus-visible:px-3 focus-visible:py-2 focus-visible:text-sm focus-visible:font-medium focus-visible:ring-2 focus-visible:ring-ring"
      >
        Skip to content
      </a>
      <SidebarProvider defaultOpen={cookieStore.get(SIDEBAR_COOKIE)?.value !== "false"}>
        <AppSidebar
          context={context}
          user={{ fullName: user.fullName, email: user.email }}
          workspaces={workspaces.map(({ id, name }) => ({ id, name }))}
          clientOf={clientOf}
        />
        <SidebarInset id="main-content" tabIndex={-1} className="focus-visible:outline-none">
          {children}
        </SidebarInset>
      </SidebarProvider>
    </NotificationsProvider>
  );
}
