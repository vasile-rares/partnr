"use client";

import { FolderIcon, SettingsIcon } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar";
import { ContextSwitcher } from "./ContextSwitcher";
import { NavUser } from "./NavUser";
import { isActivePath, portalNav, workspaceNav, type NavGroup, type NavItem } from "./nav";
import type { ShellContext, ShellUser, ShellWorkspace } from "./types";

type AppSidebarProps = {
  context: ShellContext;
  user: ShellUser;
  workspaces: ShellWorkspace[];
  clientOf: ShellWorkspace[];
};

export function AppSidebar({ context, user, workspaces, clientOf }: AppSidebarProps) {
  const pathname = usePathname();
  const groups: NavGroup[] =
    context.kind === "workspace"
      ? workspaceNav(context.workspace.id)
      : context.kind === "portal"
        ? portalNav(context.workspace.id)
        : [];
  const projects = context.kind === "none" ? [] : context.projects;
  const projectsBase =
    context.kind === "workspace"
      ? `/w/${context.workspace.id}/projects`
      : context.kind === "portal"
        ? `/portal/${context.workspace.id}/projects`
        : "";

  return (
    <Sidebar>
      <SidebarHeader>
        <ContextSwitcher context={context} workspaces={workspaces} clientOf={clientOf} />
      </SidebarHeader>

      <SidebarContent>
        <nav aria-label="Main" className="flex flex-col">
          {groups.map((group) => (
            <SidebarGroup key={group.label ?? "primary"}>
              {group.label ? <SidebarGroupLabel>{group.label}</SidebarGroupLabel> : null}
              <SidebarGroupContent>
                <SidebarMenu>
                  {group.items.map((item) => (
                    <NavEntry key={item.label} item={item} active={isActivePath(pathname, item)} />
                  ))}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          ))}

          {projects.length > 0 ? (
            <SidebarGroup>
              <SidebarGroupLabel>{context.kind === "workspace" ? "Active projects" : "Your projects"}</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  {projects.map((project) => {
                    const href = `${projectsBase}/${project.id}`;
                    const active = isActivePath(pathname, { href });
                    return (
                      <SidebarMenuItem key={project.id}>
                        <SidebarMenuButton
                          isActive={active}
                          aria-current={active ? "page" : undefined}
                          render={<Link href={href} />}
                        >
                          <FolderIcon aria-hidden />
                          <span>{project.name}</span>
                        </SidebarMenuButton>
                      </SidebarMenuItem>
                    );
                  })}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          ) : null}
        </nav>
      </SidebarContent>

      <SidebarFooter>
        {context.kind === "workspace" ? (
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton
                isActive={isActivePath(pathname, { href: `/w/${context.workspace.id}/settings` })}
                render={<Link href={`/w/${context.workspace.id}/settings`} />}
              >
                <SettingsIcon aria-hidden />
                <span>Settings</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        ) : null}
        <NavUser user={user} />
      </SidebarFooter>
    </Sidebar>
  );
}

function NavEntry({ item, active }: { item: NavItem; active: boolean }) {
  const Icon = item.icon;

  if (!item.href) {
    return (
      <SidebarMenuItem>
        <SidebarMenuButton disabled>
          <Icon aria-hidden />
          <span>
            {item.label}
            <span className="sr-only"> (coming soon)</span>
          </span>
        </SidebarMenuButton>
        <SidebarMenuBadge>Soon</SidebarMenuBadge>
      </SidebarMenuItem>
    );
  }

  return (
    <SidebarMenuItem>
      <SidebarMenuButton isActive={active} aria-current={active ? "page" : undefined} render={<Link href={item.href} />}>
        <Icon aria-hidden />
        <span>{item.label}</span>
      </SidebarMenuButton>
    </SidebarMenuItem>
  );
}
