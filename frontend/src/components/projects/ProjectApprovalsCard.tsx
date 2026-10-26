import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { ProjectApprovals } from "@/lib/projects/types";
import { GoToTab } from "./ProjectTabs";

/** Where the client's sign-offs stand; the API counts them (archived and draft deliverables do not count). */
export function ProjectApprovalsCard({ approvals }: { approvals: ProjectApprovals }) {
  const { pending, changesRequested } = approvals;

  return (
    <Card size="sm">
      <CardHeader>
        <CardTitle>Approvals</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-1">
        {pending === 0 && changesRequested === 0 ? (
          <p className="text-sm text-muted-foreground">Nothing is waiting on your client.</p>
        ) : (
          <>
            {pending > 0 ? <p className="text-lg font-medium">{pending} waiting for the client</p> : null}
            {changesRequested > 0 ? <p className="text-lg font-medium">{changesRequested} with changes requested</p> : null}
          </>
        )}
        <p className="text-sm">
          <GoToTab tab="deliverables">Open deliverables</GoToTab>
        </p>
      </CardContent>
    </Card>
  );
}
