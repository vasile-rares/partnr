import {
  CalendarIcon,
  FolderKanbanIcon,
  HouseIcon,
  ListChecksIcon,
  MessageSquareIcon,
  UsersIcon,
  VideoIcon,
  type LucideIcon,
} from "lucide-react";

export type NavItem = {
  label: string;
  icon: LucideIcon;
  /** An item without a link is a feature that does not exist yet: the sidebar shows it disabled, marked "Soon". */
  href?: string;
  /** Active only on this exact path, not on the pages below it. */
  exact?: boolean;
};

export type NavGroup = {
  label?: string;
  items: NavItem[];
};

export function workspaceNav(workspaceId: string): NavGroup[] {
  const base = `/w/${workspaceId}`;

  return [
    {
      items: [
        { label: "Home", icon: HouseIcon, href: base, exact: true },
        { label: "Projects", icon: FolderKanbanIcon, href: `${base}/projects` },
        { label: "Clients", icon: UsersIcon, href: `${base}/clients` },
      ],
    },
    {
      label: "Collaborate",
      items: [
        { label: "Messages", icon: MessageSquareIcon },
        { label: "Meetings", icon: VideoIcon },
      ],
    },
    {
      label: "Plan",
      items: [
        { label: "Calendar", icon: CalendarIcon },
        { label: "Tasks", icon: ListChecksIcon },
      ],
    },
  ];
}

export function portalNav(workspaceId: string): NavGroup[] {
  const base = `/portal/${workspaceId}`;

  return [
    {
      items: [
        { label: "Home", icon: HouseIcon, href: base, exact: true },
        { label: "Projects", icon: FolderKanbanIcon, href: `${base}/projects` },
        { label: "Messages", icon: MessageSquareIcon },
      ],
    },
  ];
}

export function isActivePath(pathname: string, { href, exact }: Pick<NavItem, "href" | "exact">): boolean {
  if (!href) {
    return false;
  }
  return pathname === href || (!exact && pathname.startsWith(`${href}/`));
}
