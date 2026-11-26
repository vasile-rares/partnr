import type { ComponentProps } from "react";
import { Badge } from "@/components/reui/badge";
import { statusLabel, type ProjectStatus } from "@/lib/projects/types";

const variants: Record<ProjectStatus, ComponentProps<typeof Badge>["variant"]> = {
  Planning: "outline",
  Active: "info-light",
  OnHold: "warning-light",
  Completed: "success-light",
};

export function ProjectStatusBadge({ status }: { status: ProjectStatus }) {
  return (
    <Badge variant={variants[status]}>
      {statusLabel(status)}
    </Badge>
  );
}
