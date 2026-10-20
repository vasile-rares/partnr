import type { Workspace } from "@/lib/workspaces/types";

export type ShellProject = { id: string; name: string };

/** Which side of the product the signed-in person is on; it decides the sidebar's menu. Only serializable data, since it crosses into a client component. */
export type ShellContext =
  | { kind: "workspace"; workspace: Pick<Workspace, "id" | "name" | "role">; projects: ShellProject[] }
  | { kind: "portal"; projects: ShellProject[] }
  | { kind: "none" };

export type ShellUser = { fullName: string; email: string };

export type ShellWorkspace = { id: string; name: string };
