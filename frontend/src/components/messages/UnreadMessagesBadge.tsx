"use client";

import { useCallback, useEffect, useState } from "react";
import { SidebarMenuBadge } from "@/components/ui/sidebar";
import { useConversationChanges } from "@/components/notifications/NotificationsProvider";
import { getJson } from "@/lib/api/client";

/** The number of unread messages next to "Messages" in the sidebar, kept current by the hub. */
export function UnreadMessagesBadge({ source }: { source: string }) {
  const [unread, setUnread] = useState(0);

  // Not aborted when it goes away (an aborted request stops the API's debugger); a late answer is just ignored.
  const refresh = useCallback(
    async (isWaiting: () => boolean) => {
      const result = await getJson<{ unread: number }>(source);
      if (isWaiting() && !("aborted" in result) && result.ok) {
        setUnread(result.data.unread);
      }
    },
    [source],
  );

  useEffect(() => {
    let mounted = true;
    const refreshNow = () => void refresh(() => mounted);
    refreshNow();
    return () => {
      mounted = false;
    };
  }, [refresh]);

  useConversationChanges(() => void refresh(() => true));

  return unread > 0 ? (
    <SidebarMenuBadge aria-label={`${unread} unread messages`}>{unread > 99 ? "99+" : unread}</SidebarMenuBadge>
  ) : null;
}
