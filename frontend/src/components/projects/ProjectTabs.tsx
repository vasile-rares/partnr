"use client";

import { useSearchParams } from "next/navigation";
import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

const tabs = [
  { value: "overview", label: "Overview" },
  { value: "deliverables", label: "Deliverables & Files" },
] as const;

export type ProjectTab = (typeof tabs)[number]["value"];

const TabContext = createContext<((tab: ProjectTab) => void) | null>(null);

const isTab = (value: string | null): value is ProjectTab => tabs.some((tab) => tab.value === value);

type ProjectTabsProps = {
  /** Shown next to a tab's name when it lists things. */
  counts: Partial<Record<ProjectTab, number>>;
  /** Rendered on the server; only the open tab is mounted. */
  panels: Record<ProjectTab, ReactNode>;
};

/** The open tab is in the address (`?tab=files`), so a link or a reload opens it, but switching never asks the server for anything. */
export function ProjectTabs({ counts, panels }: ProjectTabsProps) {
  const requested = useSearchParams().get("tab");
  const [tab, setTab] = useState<ProjectTab>(isTab(requested) ? requested : "overview");

  const select = useCallback((next: ProjectTab) => {
    setTab(next);
    const params = new URLSearchParams(window.location.search);
    if (next === "overview") {
      params.delete("tab");
    } else {
      params.set("tab", next);
    }
    window.history.replaceState(null, "", params.size > 0 ? `?${params}` : window.location.pathname);
  }, []);

  return (
    <TabContext.Provider value={select}>
      <Tabs value={tab} onValueChange={(value) => select(value as ProjectTab)}>
        <div className="overflow-x-auto border-b">
          <TabsList variant="line" className="h-10 gap-4 p-0">
            {tabs.map(({ value, label }) => (
              <TabsTrigger key={value} value={value} className="flex-none px-0.5 text-base">
                {label}
                {counts[value] ? <span className="text-xs text-muted-foreground tabular-nums">{counts[value]}</span> : null}
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
export function GoToTab({ tab, children }: { tab: ProjectTab; children: ReactNode }) {
  const select = useContext(TabContext);

  return (
    <Button type="button" variant="link" className="h-auto p-0 underline underline-offset-4" onClick={() => select?.(tab)}>
      {children}
    </Button>
  );
}
