import {
  CalendarIcon,
  FolderKanbanIcon,
  HouseIcon,
  ListChecksIcon,
  MessageSquareIcon,
  UsersIcon,
  UsersRoundIcon,
  type LucideIcon,
} from "lucide-react";

export type NavItem = {
  label: string;
  icon: LucideIcon;
  /** An item without a link is a feature that does not exist yet: the sidebar shows it disabled, marked "Soon". */
  href?: string;
  /** Active only on this exact path, not on the pages below it. */
  exact?: boolean;
  /** An API route answering `{ unread }`: the item shows that number, kept current by the hub. */
  unreadFrom?: string;
};

export type NavGroup = {
  label?: string;
  items: NavItem[];
};

export function workspaceNav(workspaceId: string): NavGroup[] {
  const base = `/w/${workspaceId}`;

  return [
    {
      label: "Platform",
      items: [
        { label: "Home", icon: HouseIcon, href: base, exact: true },
        { label: "Projects", icon: FolderKanbanIcon, href: `${base}/projects` },
        { label: "Clients", icon: UsersIcon, href: `${base}/clients` },
        { label: "Tasks", icon: ListChecksIcon },
      ],
    },
    {
      label: "Collaborate",
      items: [
        {
          label: "Messages",
          icon: MessageSquareIcon,
          href: `${base}/messages`,
          unreadFrom: `/api/workspaces/${workspaceId}/conversations/unread-count`,
        },
        { label: "Calendar", icon: CalendarIcon, href: `${base}/calendar` },
      ],
    },
    {
      label: "Manage",
      items: [{ label: "Team", icon: UsersRoundIcon, href: `${base}/team` }],
    },
  ];
}

export function clientNav(workspaceId: string): NavGroup[] {
  const base = `/w/${workspaceId}`;

  return [
    {
      items: [
        { label: "Home", icon: HouseIcon, href: base, exact: true },
        { label: "Projects", icon: FolderKanbanIcon, href: `/w/${workspaceId}/projects` },
        {
          label: "Messages",
          icon: MessageSquareIcon,
          href: `/w/${workspaceId}/messages`,
          unreadFrom: `/api/workspaces/${workspaceId}/conversations/unread-count`,
        },
        { label: "Calendar", icon: CalendarIcon, href: `/w/${workspaceId}/calendar` },
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
