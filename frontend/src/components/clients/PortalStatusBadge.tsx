import type { ComponentProps } from "react";
import { Badge } from "@/components/reui/badge";
import type { PortalStatus } from "@/lib/clients/types";

const statuses: Record<PortalStatus, { label: string; variant: ComponentProps<typeof Badge>["variant"] }> = {
  NotInvited: { label: "Not invited", variant: "outline" },
  Invited: { label: "Invited", variant: "secondary" },
  Expired: { label: "Expired", variant: "destructive-light" },
  Active: { label: "Active", variant: "success-light" },
};

export function PortalStatusBadge({ status }: { status: PortalStatus }) {
  const { label, variant } = statuses[status];

  return <Badge variant={variant}>{label}</Badge>;
}
