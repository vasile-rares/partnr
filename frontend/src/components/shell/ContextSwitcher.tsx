"use client";

import { CheckIcon, ChevronsUpDownIcon, PlusIcon } from "lucide-react";
import Link from "next/link";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SidebarMenu, SidebarMenuButton, SidebarMenuItem } from "@/components/ui/sidebar";
import type { ShellContext, ShellWorkspace } from "./types";

type ContextSwitcherProps = {
  context: ShellContext;
  /** Workspaces the person works in. */
  workspaces: ShellWorkspace[];
  /** Workspaces whose client the person is. */
  clientOf: ShellWorkspace[];
};

// The portal does not carry the firm in its URL yet, so every firm leads to the same page.
const portalHref = () => "/portal";

export function ContextSwitcher({ context, workspaces, clientOf }: ContextSwitcherProps) {
  const title = context.kind === "workspace" ? context.workspace.name : context.kind === "portal" ? "Client portal" : "Client Portal";
  const subtitle =
    context.kind === "workspace" ? context.workspace.role : context.kind === "portal" ? "Shared with you" : "Choose a workspace";

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger render={<SidebarMenuButton size="lg" className="data-popup-open:bg-sidebar-accent" />}>
            <span
              aria-hidden
              className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-sm font-medium text-primary-foreground"
            >
              {Array.from(title)[0]?.toUpperCase()}
            </span>
            <span className="grid flex-1 text-left leading-tight">
              <span className="truncate font-medium">{title}</span>
              <span className="truncate text-xs text-sidebar-foreground/70">{subtitle}</span>
            </span>
            <ChevronsUpDownIcon className="ml-auto" aria-hidden />
          </DropdownMenuTrigger>

          <DropdownMenuContent align="start" className="min-w-60">
            <DropdownMenuGroup>
              <DropdownMenuLabel>Your workspaces</DropdownMenuLabel>
              {workspaces.length === 0 ? (
                <p className="px-1.5 py-1 text-sm text-muted-foreground">No workspace yet.</p>
              ) : (
                workspaces.map((workspace) => (
                  <DropdownMenuItem key={workspace.id} render={<Link href={`/w/${workspace.id}`} />}>
                    <span className="truncate">{workspace.name}</span>
                    {context.kind === "workspace" && context.workspace.id === workspace.id ? (
                      <CheckIcon className="ml-auto" aria-label="Current workspace" />
                    ) : null}
                  </DropdownMenuItem>
                ))
              )}
            </DropdownMenuGroup>

            {clientOf.length > 0 ? (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuGroup>
                  <DropdownMenuLabel>Client of</DropdownMenuLabel>
                  {clientOf.map((workspace) => (
                    <DropdownMenuItem key={workspace.id} render={<Link href={portalHref()} />}>
                      <span className="truncate">{workspace.name}</span>
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuGroup>
              </>
            ) : null}

            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem render={<Link href="/workspaces/new" />}>
                <PlusIcon /> Create workspace
              </DropdownMenuItem>
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
