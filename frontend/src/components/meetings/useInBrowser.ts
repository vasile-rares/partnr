import { useSyncExternalStore } from "react";

const subscribe = () => () => {};

/**
 * False while the server renders and during hydration, true afterwards. Times are drawn in UTC until then and in the reader's own
 * time zone after it, so the page the server sent and the first browser render are the same and nothing mismatches.
 */
export function useInBrowser(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => true,
    () => false,
  );
}

/** The time zone to format in: UTC until the browser has taken over, then undefined, which is the reader's own. */
export function useTimeZone(): string | undefined {
  return useInBrowser() ? undefined : "UTC";
}
