import type { ComponentProps } from "react";
import { Badge } from "@/components/reui/badge";
import type { ApprovalRoundStatus } from "@/lib/deliverables/types";

const labels: Record<ApprovalRoundStatus, string> = {
  Pending: "Awaiting approval",
  Approved: "Approved",
  ChangesRequested: "Changes requested",
  Withdrawn: "Withdrawn",
};

const variants: Record<ApprovalRoundStatus, ComponentProps<typeof Badge>["variant"]> = {
  Pending: "secondary",
  Approved: "success-light",
  ChangesRequested: "destructive-light",
  Withdrawn: "outline",
};

export const approvalLabel = (status: ApprovalRoundStatus): string => labels[status];

export function ApprovalStatusBadge({ status }: { status: ApprovalRoundStatus }) {
  return <Badge variant={variants[status]}>{labels[status]}</Badge>;
}
