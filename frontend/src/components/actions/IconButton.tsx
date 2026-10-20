"use client";

import type { LucideIcon } from "lucide-react";
import type { ComponentProps } from "react";
import { Button, buttonVariants } from "@/components/ui/button";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";

// The tooltip is the short name ("Edit"); the `aria-label` should carry what it acts on ("Edit Website"), so
// several rows in a list stay distinguishable for a screen reader.
type IconProps = { icon: LucideIcon; tooltip: string };

export function IconButton({ icon: Icon, tooltip, variant = "ghost", size = "icon-sm", ...props }: IconProps & Omit<ComponentProps<typeof Button>, "children">) {
  return (
    <Tooltip>
      <TooltipTrigger render={<Button variant={variant} size={size} {...props} />}>
        <Icon />
      </TooltipTrigger>
      <TooltipContent>{tooltip}</TooltipContent>
    </Tooltip>
  );
}

export function IconLink({ icon: Icon, tooltip, ...props }: IconProps & Omit<ComponentProps<"a">, "children">) {
  return (
    <Tooltip>
      <TooltipTrigger render={<a {...props} className={buttonVariants({ variant: "ghost", size: "icon-sm" })} />}>
        <Icon />
      </TooltipTrigger>
      <TooltipContent>{tooltip}</TooltipContent>
    </Tooltip>
  );
}
