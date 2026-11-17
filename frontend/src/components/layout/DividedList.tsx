import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";

/** The bordered list the pages use for rows: items go in as `<li>`, with their own padding. */
export function DividedList({ ordered = false, className, ...props }: { ordered?: boolean } & ComponentProps<"ul">) {
  const Tag = ordered ? "ol" : "ul";
  return <Tag className={cn("flex flex-col divide-y rounded-lg border", className)} {...(props as ComponentProps<"ol">)} />;
}
