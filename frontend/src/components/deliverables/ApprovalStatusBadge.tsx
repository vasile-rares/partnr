import { Badge } from "@/components/ui/badge";
import type { ApprovalRoundStatus } from "@/lib/deliverables/types";

const labels: Record<ApprovalRoundStatus, string> = {
  Pending: "Awaiting approval",
  Approved: "Approved",
  ChangesRequested: "Changes requested",
  Withdrawn: "Withdrawn",
};

const variants: Record<ApprovalRoundStatus, "default" | "secondary" | "destructive" | "outline"> = {
  Pending: "secondary",
  Approved: "default",
  ChangesRequested: "destructive",
  Withdrawn: "outline",
};

export const approvalLabel = (status: ApprovalRoundStatus): string => labels[status];

export function ApprovalStatusBadge({ status }: { status: ApprovalRoundStatus }) {
  return <Badge variant={variants[status]}>{labels[status]}</Badge>;
}
