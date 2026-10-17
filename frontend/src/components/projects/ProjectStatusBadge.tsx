import type { ComponentProps } from "react";
import { Badge } from "@/components/ui/badge";
import { statusLabel, type ProjectStatus } from "@/lib/projects/types";

const variants: Record<ProjectStatus, ComponentProps<typeof Badge>["variant"]> = {
  Planning: "outline",
  Active: "default",
  OnHold: "secondary",
  Completed: "secondary",
};

export function ProjectStatusBadge({ status }: { status: ProjectStatus }) {
  return <Badge variant={variants[status]}>{statusLabel(status)}</Badge>;
}
