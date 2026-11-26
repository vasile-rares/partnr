"use client";

import { BellIcon } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { timeAgo } from "@/lib/format";
import type { NotificationItem } from "@/lib/notifications/types";
import { cn } from "@/lib/utils";
import { useNotifications } from "./NotificationsProvider";

// A path from the API is only followed when it stays inside this app: router.push must never get a URL from elsewhere.
const isAppPath = (path: string) => path.startsWith("/") && !path.startsWith("//");

/** The bell, in the sidebar's header or, while the sidebar is out of sight, in the top bar: the number of unread notifications and, when opened, the account's latest ones. The data lives in the provider. */
export function NotificationsBell({ placement }: { placement: "sidebar" | "topbar" }) {
  const router = useRouter();
  const { unread, items, failed, setListOpen, loadList, markRead, markAllRead } = useNotifications();
  const [open, setOpen] = useState(false);

  function onOpenChange(next: boolean) {
    setOpen(next);
    setListOpen(next);
    if (next) {
      void loadList();
    }
  }

  async function openItem(item: NotificationItem) {
    // Marked before leaving: the next page's bell shows the count the provider holds, so it has to be right by then.
    const result = await markRead(item);
    if (result === "gone") {
      void loadList(); // taken back in the meantime: it is not there any more
      return;
    }

    onOpenChange(false);
    if (isAppPath(item.path)) {
      router.push(item.path);
    }
  }

  const label = unread ? `Notifications, ${unread} unread` : "Notifications";

  return (
    <Popover open={open} onOpenChange={onOpenChange}>
      <PopoverTrigger
        render={<Button type="button" variant="ghost" size="icon-sm" aria-label={label} className="notifications-bell relative" />}
      >
        <BellIcon />
        {unread ? (
          <span
            aria-hidden
            className="notifications-bell-count absolute -top-1 -right-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-primary px-1 text-[10px] leading-none font-medium text-primary-foreground"
          >
            {unread > 9 ? "9+" : unread}
          </span>
        ) : null}
      </PopoverTrigger>
      <PopoverContent
        side={placement === "sidebar" ? "right" : "bottom"}
        align={placement === "sidebar" ? "start" : "end"}
        className="notifications-popover w-80 gap-0 p-0 sm:w-96">
        <div className="notifications-header flex items-center justify-between gap-2 border-b px-3 py-2">
          <h2 className="font-medium">Notifications</h2>
          <Button type="button" variant="ghost" size="xs" disabled={!unread} onClick={markAllRead}>
            Mark all as read
          </Button>
        </div>
        <NotificationList items={items} failed={failed} onOpen={openItem} />
      </PopoverContent>
    </Popover>
  );
}

function NotificationList({
  items,
  failed,
  onOpen,
}: {
  items: NotificationItem[] | null;
  failed: boolean;
  onOpen: (item: NotificationItem) => void;
}) {
  if (failed) {
    return <p className="notifications-message px-3 py-6 text-center text-sm text-muted-foreground">Couldn&apos;t load your notifications. Try again in a moment.</p>;
  }
  if (items === null) {
    return <p className="notifications-message px-3 py-6 text-center text-sm text-muted-foreground">Loading…</p>;
  }
  if (items.length === 0) {
    return <p className="notifications-message px-3 py-6 text-center text-sm text-muted-foreground">You&apos;re all caught up.</p>;
  }

  return (
    <ul className="notifications-list max-h-96 divide-y overflow-y-auto">
      {items.map((item) => (
        <li key={item.id} className="notifications-item">
          <button
            type="button"
            onClick={() => onOpen(item)}
            className="flex w-full items-start gap-2 px-3 py-2.5 text-left outline-none hover:bg-muted focus-visible:bg-muted"
          >
            <span
              aria-hidden
              className={cn("mt-1.5 size-2 shrink-0 rounded-full", item.isRead ? "bg-transparent" : "bg-primary")}
            />
            <span className="flex min-w-0 flex-col gap-0.5">
              <span className={cn("text-sm", item.isRead ? "text-muted-foreground" : "font-medium")}>
                {item.title}
                {item.isRead ? null : <span className="sr-only"> (unread)</span>}
              </span>
              <span className="text-xs text-muted-foreground">
                {item.workspaceName} · {timeAgo(item.createdAtUtc)}
              </span>
            </span>
          </button>
        </li>
      ))}
    </ul>
  );
}
