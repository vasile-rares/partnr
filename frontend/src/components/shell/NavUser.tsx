"use client";

import { ChevronsUpDownIcon, LogOutIcon, UserIcon } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTransition } from "react";
import { ThemeRadioGroup } from "@/components/theme-toggle";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
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
import { postAuth } from "@/lib/auth/client";
import { initials } from "@/lib/format";
import type { ShellUser } from "./types";

export function NavUser({ user }: { user: ShellUser }) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger render={<SidebarMenuButton size="lg" className="data-popup-open:bg-sidebar-accent" />}>
            <Avatar>
              <AvatarFallback>{initials(user.fullName)}</AvatarFallback>
            </Avatar>
            <span className="grid flex-1 text-left leading-tight">
              <span className="truncate font-medium">{user.fullName}</span>
              <span className="truncate text-xs text-sidebar-foreground/70">{user.email}</span>
            </span>
            <ChevronsUpDownIcon className="ml-auto" aria-hidden />
          </DropdownMenuTrigger>

          <DropdownMenuContent side="top" align="start" className="min-w-56">
            <DropdownMenuGroup>
              <DropdownMenuItem render={<Link href="/account" />}>
                <UserIcon /> Account
              </DropdownMenuItem>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuLabel>Theme</DropdownMenuLabel>
              <ThemeRadioGroup />
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuItem
                disabled={pending}
                onClick={() =>
                  startTransition(async () => {
                    await postAuth("logout");
                    router.replace("/login");
                    router.refresh();
                  })
                }
              >
                <LogOutIcon /> Sign out
              </DropdownMenuItem>
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}
