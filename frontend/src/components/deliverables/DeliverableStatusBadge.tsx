import { Badge } from "@/components/ui/badge";
import type { DeliverableStatus } from "@/lib/deliverables/types";

export function DeliverableStatusBadge({ status }: { status: DeliverableStatus }) {
  return <Badge variant={status === "Shared" ? "default" : "outline"}>{status}</Badge>;
}
