"use client";

import type { ReactNode } from "react";
import { useSidebar } from "@/components/ui/sidebar";

// The trigger and the bell live in the sidebar's header, which stays on screen as a rail of icons when it is collapsed. Only on a phone,
// where the sidebar is a sheet that is closed, the top bar of the page shows them instead.

/** While the sidebar is on screen (open or collapsed to icons). */
export function WhenSidebarShown({ children }: { children: ReactNode }) {
  const { isMobile } = useSidebar();

  return isMobile ? null : children;
}

/** While the sidebar is a sheet, as on a phone. */
export function WhenSidebarHidden({ children }: { children: ReactNode }) {
  const { isMobile } = useSidebar();

  return isMobile ? children : null;
}
