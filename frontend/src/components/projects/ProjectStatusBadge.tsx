import type { ComponentProps } from "react";
import { Badge } from "@/components/ui/badge";
import { statusLabel, type ProjectStatus } from "@/lib/projects/types";

const variants: Record<ProjectStatus, ComponentProps<typeof Badge>["variant"]> = {
  Planning: "outline",
  Active: "default",
  OnHold: "secondary",
  Completed: "secondary",
};

/** The project's status as a small label (the text carries the meaning, the colour only helps). */
export function ProjectStatusBadge({ status }: { status: ProjectStatus }) {
  return <Badge variant={variants[status]}>{statusLabel(status)}</Badge>;
}
