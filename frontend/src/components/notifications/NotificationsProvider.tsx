"use client";

import { HttpTransportType, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { getJson, sendJson } from "@/lib/api/client";
import type { NotificationItem, Notifications } from "@/lib/notifications/types";

type NotificationsState = {
  /** Null until the first answer. */
  unread: number | null;
  /** Null until the list was asked for (the bell is opened). */
  items: NotificationItem[] | null;
  failed: boolean;
  /** The bell tells whether its list is on screen, so a push also reloads the list then, and only then. */
  setListOpen: (open: boolean) => void;
  loadList: () => Promise<void>;
  /** "gone": it was taken back in the meantime (404). "failed": the answer was lost, the item is not marked. */
  markRead: (item: NotificationItem) => Promise<"ok" | "gone" | "failed">;
  markAllRead: () => Promise<void>;
};

const NotificationsContext = createContext<NotificationsState | null>(null);

export function useNotifications(): NotificationsState {
  const state = useContext(NotificationsContext);
  if (!state) {
    throw new Error("useNotifications needs a NotificationsProvider above it.");
  }
  return state;
}

// The wait before each try to (re)connect, in milliseconds; the last one repeats for as long as it takes.
const RETRY_DELAYS = [0, 2_000, 10_000, 30_000];
const retryDelay = (attempt: number) => RETRY_DELAYS[Math.min(attempt, RETRY_DELAYS.length - 1)];

/**
 * The account's inbox for the whole signed-in area: one connection to the hub for as long as the layout stays mounted
 * (a page's top bar is remounted on every navigation, so the connection cannot live there). The server only says that
 * something changed; what changed is asked of the REST routes. Nothing polls: after a connection (re)starts the state is
 * loaded again, because what happened while it was down was never sent.
 */
export function NotificationsProvider({ children }: { children: ReactNode }) {
  const [unread, setUnread] = useState<number | null>(null);
  const [items, setItems] = useState<NotificationItem[] | null>(null);
  const [failed, setFailed] = useState(false);
  const listOpen = useRef(false);

  const loadList = useCallback(async () => {
    const result = await getJson<Notifications>("/api/notifications");
    if ("aborted" in result) {
      return;
    }
    if (result.ok) {
      setItems(result.data.items);
      setUnread(result.data.unread);
      setFailed(false);
    } else {
      setFailed(true);
    }
  }, []);

  // Not aborted when it is no longer wanted: a cancelled request makes the API throw OperationCanceledException,
  // which stops a debugger. The query is tiny, so a late answer is simply ignored.
  const refresh = useCallback(
    async (isWaiting: () => boolean) => {
      if (listOpen.current) {
        await loadList();
        return;
      }
      const result = await getJson<{ unread: number }>("/api/notifications/unread-count");
      if (isWaiting() && !("aborted" in result) && result.ok) {
        setUnread(result.data.unread);
      }
    },
    [loadList],
  );

  useEffect(() => {
    let active = true;
    let retryTimer: number | undefined;
    const isWaiting = () => active;
    const refreshNow = () => void refresh(isWaiting);

    const connection = new HubConnectionBuilder()
      // WebSockets only: the web app's proxy cuts a long HTTP request at 30 s, which long polling would hit.
      .withUrl("/api/hubs/notifications", { transport: HttpTransportType.WebSockets, skipNegotiation: true })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount) })
      .configureLogging(LogLevel.None) // a failed start is retried below, and dev's double mount would log a harmless "stopped"
      .build();
    connection.on("inboxChanged", () => active && refreshNow());
    connection.onreconnected(() => active && refreshNow());

    // Automatic reconnection only covers a connection that was up; the first connection is retried here.
    const connect = async (attempt: number): Promise<void> => {
      try {
        await connection.start();
      } catch {
        if (active) {
          retryTimer = window.setTimeout(() => void connect(attempt + 1), retryDelay(attempt + 1));
        }
        return;
      }
      if (active) {
        refreshNow();
      }
    };

    refreshNow(); // what is there now, without waiting for the connection
    void connect(0);

    return () => {
      active = false;
      window.clearTimeout(retryTimer);
      void connection.stop();
    };
  }, [refresh]);

  const setListOpen = useCallback((open: boolean) => {
    listOpen.current = open;
  }, []);

  const markRead = useCallback(async (target: NotificationItem) => {
    const result = await sendJson("POST", `/api/notifications/${target.id}/read`);
    if (!result.ok) {
      return result.status === 404 ? "gone" : "failed";
    }
    setItems((current) => current?.map((item) => (item.id === target.id ? { ...item, isRead: true } : item)) ?? null);
    if (!target.isRead) {
      setUnread((count) => (count === null ? count : Math.max(0, count - 1)));
    }
    return "ok";
  }, []);

  const markAllRead = useCallback(async () => {
    const result = await sendJson("POST", "/api/notifications/read-all");
    if (result.ok) {
      setItems((current) => current?.map((item) => ({ ...item, isRead: true })) ?? null);
      setUnread(0);
    }
  }, []);

  const value = useMemo(
    () => ({ unread, items, failed, setListOpen, loadList, markRead, markAllRead }),
    [unread, items, failed, setListOpen, loadList, markRead, markAllRead],
  );

  return <NotificationsContext.Provider value={value}>{children}</NotificationsContext.Provider>;
}
