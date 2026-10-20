import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { Deliverable } from "@/lib/deliverables/types";
import { GoToTab } from "./ProjectTabs";

/** Where the client's sign-offs stand, from the deliverables the page already has (archived ones do not count). */
export function ProjectApprovalsCard({ deliverables }: { deliverables: Deliverable[] }) {
  const open = deliverables.filter((deliverable) => deliverable.archivedAtUtc === null);
  const waiting = open.filter((deliverable) => deliverable.approvalStatus === "Pending").length;
  const changes = open.filter((deliverable) => deliverable.approvalStatus === "ChangesRequested").length;

  return (
    <Card size="sm">
      <CardHeader>
        <CardTitle>Approvals</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-1">
        {waiting === 0 && changes === 0 ? (
          <p className="text-sm text-muted-foreground">Nothing is waiting on your client.</p>
        ) : (
          <>
            {waiting > 0 ? <p className="text-lg font-medium">{waiting} waiting for the client</p> : null}
            {changes > 0 ? <p className="text-lg font-medium">{changes} with changes requested</p> : null}
          </>
        )}
        <p className="text-sm">
          <GoToTab tab="deliverables">Open deliverables</GoToTab>
        </p>
      </CardContent>
    </Card>
  );
}
