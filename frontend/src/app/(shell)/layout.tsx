import type { ReactNode } from "react";
import { AppShell } from "@/components/shell/AppShell";

// Pages that belong to the person, not to a workspace (account, workspace list): the sidebar has no menu of its own here.
export default function ShellLayout({ children }: { children: ReactNode }) {
  return <AppShell context={{ kind: "none" }}>{children}</AppShell>;
}
