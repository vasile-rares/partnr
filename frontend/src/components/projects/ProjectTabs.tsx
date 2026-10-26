"use client";

import { useSearchParams } from "next/navigation";
import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";

export type ProjectTabItem = {
  value: string;
  label: string;
  /** Shown next to the name when it lists things; nothing for 0. */
  count?: number;
  /** For a count that asks something of the reader, such as approvals waiting. */
  highlight?: boolean;
};

type ProjectTabsProps = {
  /** The first one is open when the address names none; only tabs worth showing belong here. */
  tabs: ProjectTabItem[];
  /** Rendered on the server, by tab value; only the open tab is mounted. */
  panels: Record<string, ReactNode>;
};

const TabContext = createContext<((tab: string) => void) | null>(null);

/** The open tab is in the address (`?tab=files`), so a link or a reload opens it, but switching never asks the server for anything. */
export function ProjectTabs({ tabs, panels }: ProjectTabsProps) {
  const first = tabs[0].value;
  const requested = useSearchParams().get("tab") ?? first;
  const [tab, setTab] = useState(tabs.some((item) => item.value === requested) ? requested : first);

  const select = useCallback(
    (next: string) => {
      setTab(next);
      const params = new URLSearchParams(window.location.search);
      if (next === first) {
        params.delete("tab");
      } else {
        params.set("tab", next);
      }
      window.history.replaceState(null, "", params.size > 0 ? `?${params}` : window.location.pathname);
    },
    [first],
  );

  return (
    <TabContext.Provider value={select}>
      <Tabs value={tab} onValueChange={(value) => select(value as string)}>
        <div className="overflow-x-auto border-b">
          <TabsList variant="line" className="h-10 gap-4 p-0">
            {tabs.map(({ value, label, count, highlight }) => (
              <TabsTrigger key={value} value={value} className="flex-none px-0.5 text-base">
                {label}
                {count ? (
                  <span
                    className={cn(
                      "text-xs tabular-nums",
                      highlight ? "rounded-full bg-primary px-1.5 py-0.5 text-primary-foreground" : "text-muted-foreground",
                    )}
                  >
                    {count}
                  </span>
                ) : null}
              </TabsTrigger>
            ))}
          </TabsList>
        </div>
        {tabs.map(({ value }) => (
          <TabsContent key={value} value={value} className="pt-8 text-base">
            {panels[value]}
          </TabsContent>
        ))}
      </Tabs>
    </TabContext.Provider>
  );
}

/** A link-looking button that opens another tab of the project page it sits in. */
export function GoToTab({ tab, children }: { tab: string; children: ReactNode }) {
  const select = useContext(TabContext);

  return (
    <Button type="button" variant="link" className="h-auto p-0 underline underline-offset-4" onClick={() => select?.(tab)}>
      {children}
    </Button>
  );
}
