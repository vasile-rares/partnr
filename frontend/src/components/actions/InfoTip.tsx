"use client";

import { InfoIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";

type InfoTipProps = {
  /** Names the button for screen readers. */
  label: string;
  icon?: ReactNode;
  /** A short word beside the icon, for a label such as "Private" that is worth reading before it is opened. */
  text?: string;
  children: ReactNode;
};

/** A small icon that explains something the page has no room for. A popover, not a tooltip: it opens on a tap too. */
export function InfoTip({ label, icon, text, children }: InfoTipProps) {
  return (
    <Popover>
      <PopoverTrigger
        openOnHover
        render={
          <Button
            type="button"
            variant="ghost"
            size={text ? "xs" : "icon-sm"}
            aria-label={label}
            className="-my-1 text-muted-foreground"
          />
        }
      >
        {icon ?? <InfoIcon />}
        {text}
      </PopoverTrigger>
      <PopoverContent className="w-auto max-w-72 text-sm">{children}</PopoverContent>
    </Popover>
  );
}
