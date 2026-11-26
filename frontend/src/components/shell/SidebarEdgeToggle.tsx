"use client";

import { useSidebar } from "@/components/ui/sidebar";

/** A line just inside the page card, next to the sidebar, that bends into an arrow on hover and focus, pointing where the sidebar will go. */
export function SidebarEdgeToggle() {
  const { state, toggleSidebar } = useSidebar();
  // Square ends meet in the middle so the resting line is straight; the dimming is on the wrapper, so the halves are not see-through.
  const bar = "block h-2.5 w-0.5 bg-sidebar-foreground transition-[rotate] duration-200 ease-out";

  return (
    <button
      type="button"
      data-state={state}
      aria-label="Toggle sidebar"
      aria-expanded={state === "expanded"}
      onClick={toggleSidebar}
      className="sidebar-edge-toggle group/edge absolute top-1/2 -right-4 z-20 hidden h-16 w-4 -translate-y-1/2 cursor-w-resize flex-col items-center justify-center outline-hidden [--tilt-bottom:-38deg] [--tilt-top:38deg] data-[state=collapsed]:cursor-e-resize data-[state=collapsed]:[--tilt-bottom:38deg] data-[state=collapsed]:[--tilt-top:-38deg] md:flex"
    >
      <span
        aria-hidden
        className="flex flex-col items-center opacity-30 transition-opacity duration-200 ease-out group-hover/edge:opacity-80 group-focus-visible/edge:opacity-80"
      >
        <span
          className={`${bar} origin-bottom rounded-t-full group-hover/edge:rotate-(--tilt-top) group-focus-visible/edge:rotate-(--tilt-top)`}
        />
        <span
          className={`${bar} -mt-px origin-top rounded-b-full group-hover/edge:rotate-(--tilt-bottom) group-focus-visible/edge:rotate-(--tilt-bottom)`}
        />
      </span>
    </button>
  );
}
